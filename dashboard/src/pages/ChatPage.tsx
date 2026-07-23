import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { useNavigate } from 'react-router-dom';
import { apiFetch } from '../api/client';
import ClipboardButton from '../components/ClipboardButton';
import { translateTooltip } from '../i18n';
import type { AmbiguitySignal, ChatMessageRequest, ChatOptionsResponse, ChatPromptPreviewResponse, ChatRequest, ChatResponseResult, ChatStreamEvent, ChatTelemetry, ChatToolCall, DatabaseDetail, SettingsReadResponse, VerifiedAnswer } from '../types';

interface ChatUiMessage {
  Id: string;
  Role: 'user' | 'assistant';
  Content: string;
  Telemetry: ChatTelemetry | null;
  ToolCalls: ChatToolCall[];
  VerifiedAnswer: VerifiedAnswer | null;
  Ambiguities: AmbiguitySignal[];
  ExecutionPath: string | null;
  CapabilityNotice: string | null;
  LocalOnly?: boolean;
  PromptPreview?: ChatPromptPreviewResponse | null;
}

interface ParsedSseFrame {
  event: string;
  data: string;
}

interface SlashCommandDefinition {
  Command: string;
  Label: string;
  Description: string;
}

interface PromptPreviewLocalMessage {
  Content: string;
  Preview: ChatPromptPreviewResponse | null;
}

const slashCommands: SlashCommandDefinition[] = [
  { Command: '/help', Label: 'Help', Description: 'Show available chat commands.' },
  { Command: '/clear', Label: 'Clear', Description: 'Clear the visible conversation and the history sent to the model.' },
  { Command: '/context', Label: 'Context', Description: 'Show what context the next chat request will use.' },
  { Command: '/prompt', Label: 'Prompt', Description: 'Show the prepared system and database context prompts.' },
];

export default function ChatPage() {
  const navigate = useNavigate();
  const transcriptRef = useRef<HTMLDivElement | null>(null);
  const transcriptContentRef = useRef<HTMLDivElement | null>(null);
  const stickToBottomRef = useRef(true);
  const scrollFrameRef = useRef<number | null>(null);
  const activeRequestRef = useRef<AbortController | null>(null);
  const [options, setOptions] = useState<ChatOptionsResponse | null>(null);
  const [databaseId, setDatabaseId] = useState('');
  const [providerId, setProviderId] = useState('');
  const [streaming, setStreaming] = useState(true);
  const [messages, setMessages] = useState<ChatUiMessage[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => { loadOptions(); }, []);
  useLayoutEffect(() => { scrollTranscriptToBottom(); }, [messages]);

  useEffect(() => {
    const transcript = transcriptRef.current;
    const content = transcriptContentRef.current;
    if (!transcript || !content || typeof ResizeObserver === 'undefined') return;

    const observer = new ResizeObserver(() => {
      if (stickToBottomRef.current) {
        scrollTranscriptToBottom();
      } else {
        clampTranscriptScroll();
      }
    });

    observer.observe(content);
    observer.observe(transcript);

    return () => {
      observer.disconnect();
    };
  }, []);

  useEffect(() => {
    return () => {
      if (scrollFrameRef.current != null) {
        cancelAnimationFrame(scrollFrameRef.current);
      }
      activeRequestRef.current?.abort();
    };
  }, []);

  const selectedProvider = useMemo(
    () => options?.Providers.find(provider => provider.Id === providerId) || null,
    [options, providerId]
  );
  const selectedDatabase = useMemo(
    () => options?.Databases.find(database => database.Id === databaseId) || null,
    [options, databaseId]
  );
  const slashCommandQuery = getSlashCommandQuery(input);
  const filteredSlashCommands = useMemo(
    () => slashCommandQuery
      ? slashCommands.filter(command => command.Command.startsWith(slashCommandQuery) || command.Label.toLowerCase().startsWith(slashCommandQuery.slice(1)))
      : [],
    [slashCommandQuery]
  );

  async function loadOptions() {
    try {
      const response = await apiFetch('/v1/chat/options');
      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) { setError('Failed to load chat options.'); return; }

      const data: ChatOptionsResponse = await response.json();
      setOptions(data);
      setDatabaseId(data.Databases[0]?.Id || '');
      setProviderId(selectAvailableProviderId(data));
      setStreaming(data.DefaultStreaming);
      setError('');
    } catch {
      setError('Could not connect to server.');
    }
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    const trimmed = input.trim();
    if (!trimmed || sending) return;
    if (trimmed.startsWith('/')) {
      await runSlashCommand(trimmed);
      return;
    }

    const userMessage: ChatUiMessage = {
      Id: crypto.randomUUID(),
      Role: 'user',
      Content: trimmed,
      Telemetry: null,
      ToolCalls: [],
      VerifiedAnswer: null,
      Ambiguities: [],
      ExecutionPath: null,
      CapabilityNotice: null,
    };
    const assistantId = crypto.randomUUID();
    const assistantMessage: ChatUiMessage = {
      Id: assistantId,
      Role: 'assistant',
      Content: '',
      Telemetry: null,
      ToolCalls: [],
      VerifiedAnswer: null,
      Ambiguities: [],
      ExecutionPath: null,
      CapabilityNotice: null,
    };

    const nextMessages = [...messages, userMessage, assistantMessage];
    setMessages(nextMessages);
    setInput('');
    setSending(true);
    setError('');
    const controller = new AbortController();
    activeRequestRef.current = controller;

    const request: ChatRequest = {
      DatabaseId: databaseId,
      ProviderId: providerId,
      Messages: [...messages, userMessage].filter(message => !message.LocalOnly).map(toRequestMessage),
      Streaming: streaming,
    };

    try {
      if (streaming) {
        await sendStreaming(request, assistantId, controller.signal);
      } else {
        await sendNonStreaming(request, assistantId, controller.signal);
      }
    } finally {
      if (activeRequestRef.current === controller) {
        activeRequestRef.current = null;
      }
      setSending(false);
    }
  }

  function stopGeneration() {
    activeRequestRef.current?.abort();
  }

  async function runSlashCommand(value: string) {
    const command = value.split(/\s+/)[0].toLowerCase();
    setInput('');
    setError('');

    if (command === '/clear') {
      clearConversation();
      return;
    }

    if (command === '/help') {
      appendLocalAssistantMessage(buildSlashHelpMessage());
      return;
    }

    if (command === '/context') {
      appendLocalAssistantMessage(await buildContextUsageMessage());
      return;
    }

    if (command === '/prompt') {
      const promptMessage = await buildPromptPreviewMessage();
      appendLocalAssistantMessage(promptMessage.Content, promptMessage.Preview);
      return;
    }

    appendLocalAssistantMessage(buildUnknownCommandMessage(command));
  }

  async function sendNonStreaming(request: ChatRequest, assistantId: string, signal: AbortSignal) {
    try {
      const response = await apiFetch('/v1/chat', {
        method: 'POST',
        body: JSON.stringify(request),
        signal,
      });

      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) {
        const message = await response.text();
        failAssistant(assistantId, message || 'Chat request failed.');
        return;
      }

      const result: ChatResponseResult = await response.json();
      setMessages(previous => previous.map(message => message.Id === assistantId
        ? { ...message, Content: result.Message || '', Telemetry: result.Telemetry, ToolCalls: result.ToolCalls || [], VerifiedAnswer: result.VerifiedAnswer, Ambiguities: result.Ambiguities || [], ExecutionPath: result.ExecutionPath, CapabilityNotice: result.CapabilityNotice }
        : message));
    } catch (ex) {
      if (isAbortError(ex)) {
        stopAssistant(assistantId);
        return;
      }
      failAssistant(assistantId, 'Could not connect to server.');
    }
  }

  async function sendStreaming(request: ChatRequest, assistantId: string, signal: AbortSignal) {
    try {
      const response = await apiFetch('/v1/chat/stream', {
        method: 'POST',
        body: JSON.stringify(request),
        signal,
      });

      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok || !response.body) {
        const message = await response.text();
        failAssistant(assistantId, message || 'Chat stream failed.');
        return;
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      let doneReading = false;

      while (!doneReading) {
        const result = await reader.read();
        doneReading = result.done;
        if (result.value) {
          buffer += decoder.decode(result.value, { stream: !doneReading });
          const frames = buffer.split('\n\n');
          buffer = frames.pop() || '';
          frames.forEach(frame => handleStreamFrame(frame, assistantId));
        }
      }

      if (buffer.trim()) {
        handleStreamFrame(buffer, assistantId);
      }
    } catch (ex) {
      if (isAbortError(ex)) {
        stopAssistant(assistantId);
        return;
      }
      failAssistant(assistantId, 'Could not connect to server.');
    }
  }

  function handleStreamFrame(frame: string, assistantId: string) {
    const parsed = parseSseFrame(frame);
    if (!parsed.data) return;

    const event: ChatStreamEvent = JSON.parse(parsed.data);
    if (event.EventType === 'token' && event.Delta) {
      setMessages(previous => previous.map(message => message.Id === assistantId
        ? { ...message, Content: message.Content + event.Delta }
        : message));
    } else if (event.EventType === 'completed') {
      setMessages(previous => previous.map(message => message.Id === assistantId
        ? { ...message, Content: event.Message || message.Content, Telemetry: event.Telemetry, VerifiedAnswer: event.VerifiedAnswer || message.VerifiedAnswer, Ambiguities: event.Ambiguities || message.Ambiguities, ExecutionPath: event.ExecutionPath || message.ExecutionPath, CapabilityNotice: event.CapabilityNotice || message.CapabilityNotice }
        : message));
    } else if ((event.EventType === 'tool_started' || event.EventType === 'tool_completed') && event.ToolCall) {
      setMessages(previous => previous.map(message => message.Id === assistantId
        ? { ...message, ToolCalls: mergeToolCall(message.ToolCalls, event.ToolCall as ChatToolCall), ExecutionPath: event.ExecutionPath || message.ExecutionPath, CapabilityNotice: event.CapabilityNotice || message.CapabilityNotice }
        : message));
    } else if (event.EventType === 'error') {
      failAssistant(assistantId, event.Error || 'Chat stream failed.');
    }
  }

  function parseSseFrame(frame: string): ParsedSseFrame {
    const lines = frame.replace(/\r/g, '').split('\n');
    const dataLines: string[] = [];
    let eventName = 'message';

    lines.forEach(line => {
      if (line.startsWith('event:')) {
        eventName = line.slice(6).trim();
      } else if (line.startsWith('data:')) {
        dataLines.push(line.slice(5).trimStart());
      }
    });

    return {
      event: eventName,
      data: dataLines.join('\n'),
    };
  }

  function failAssistant(assistantId: string, message: string) {
    setError(message);
    setMessages(previous => previous.map(item => item.Id === assistantId
      ? { ...item, Content: 'Request failed: ' + message }
      : item));
  }

  function stopAssistant(assistantId: string) {
    setError('');
    setMessages(previous => previous.map(item => {
      if (item.Id !== assistantId) return item;
      return {
        ...item,
        Content: item.Content || 'Generation stopped.',
        CapabilityNotice: item.Content ? 'Generation stopped.' : item.CapabilityNotice,
      };
    }));
  }

  function mergeToolCall(existing: ChatToolCall[], next: ChatToolCall) {
    const index = existing.findIndex(toolCall => toolCall.Id === next.Id);
    if (index < 0) return [...existing, next];
    return existing.map(toolCall => toolCall.Id === next.Id ? next : toolCall);
  }

  function handleComposerKeyDown(event: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key !== 'Enter') return;
    if (event.shiftKey || event.ctrlKey || event.metaKey || event.altKey) return;

    event.preventDefault();
    event.currentTarget.form?.requestSubmit();
  }

  function resetConversation() {
    setMessages([]);
    setInput('');
    setError('');
    stickToBottomRef.current = true;
    scrollTranscriptToBottom();
  }

  function clearConversation() {
    activeRequestRef.current?.abort();
    activeRequestRef.current = null;
    setSending(false);
    resetConversation();
  }

  function appendLocalAssistantMessage(content: string, promptPreview: ChatPromptPreviewResponse | null = null) {
    const message: ChatUiMessage = {
      Id: crypto.randomUUID(),
      Role: 'assistant',
      Content: content,
      Telemetry: null,
      ToolCalls: [],
      VerifiedAnswer: null,
      Ambiguities: [],
      ExecutionPath: null,
      CapabilityNotice: null,
      LocalOnly: true,
      PromptPreview: promptPreview,
    };

    stickToBottomRef.current = true;
    setMessages(previous => [...previous, message]);
  }

  async function buildContextUsageMessage() {
    if (!databaseId) return '### Context Usage\n\nNo database is selected.';

    let detail: DatabaseDetail | null = null;
    let settings: SettingsReadResponse | null = null;
    let loadWarning = '';

    try {
      const [detailResponse, settingsResponse] = await Promise.all([
        apiFetch(`/v1/database/${databaseId}`),
        apiFetch('/v1/settings'),
      ]);

      if (detailResponse.status === 401 || settingsResponse.status === 401) {
        navigate('/login');
        return '### Context Usage\n\nSign in is required to inspect context usage.';
      }

      if (detailResponse.ok) {
        detail = await detailResponse.json();
      } else {
        loadWarning = 'Database detail could not be loaded.';
      }

      if (settingsResponse.ok) {
        settings = await settingsResponse.json();
      }
    } catch {
      loadWarning = 'Context detail could not be loaded from the server.';
    }

    return buildContextUsageMarkdown({
      database: selectedDatabase,
      detail,
      provider: selectedProvider,
      settings,
      messages,
      streaming,
      loadWarning,
    });
  }

  async function buildPromptPreviewMessage(): Promise<PromptPreviewLocalMessage> {
    if (!databaseId) return { Content: '### Prompt Preview\n\nNo database is selected.', Preview: null };
    if (!providerId) return { Content: '### Prompt Preview\n\nNo provider is selected.', Preview: null };

    const request: ChatRequest = {
      DatabaseId: databaseId,
      ProviderId: providerId,
      Messages: messages.filter(message => !message.LocalOnly).map(toRequestMessage),
      Streaming: streaming,
    };

    try {
      const response = await apiFetch('/v1/chat/prompt', {
        method: 'POST',
        body: JSON.stringify(request),
      });

      if (response.status === 401) {
        navigate('/login');
        return { Content: '### Prompt Preview\n\nSign in is required to inspect the prompt.', Preview: null };
      }

      if (!response.ok) {
        const message = await response.text();
        return { Content: ['### Prompt Preview', '', message || 'Prompt preview could not be prepared.'].join('\n'), Preview: null };
      }

      const preview: ChatPromptPreviewResponse = await response.json();
      return { Content: buildPromptPreviewMarkdown(preview), Preview: preview };
    } catch {
      return { Content: '### Prompt Preview\n\nCould not connect to the server.', Preview: null };
    }
  }

  function handleDatabaseChanged(nextDatabaseId: string) {
    if (nextDatabaseId === databaseId) return;
    setDatabaseId(nextDatabaseId);
    resetConversation();
  }

  function handleProviderChanged(nextProviderId: string) {
    if (nextProviderId === providerId) return;
    setProviderId(nextProviderId);
    resetConversation();
  }

  function toRequestMessage(message: ChatUiMessage): ChatMessageRequest {
    return {
      Role: message.Role,
      Content: message.Content,
    };
  }

  function scrollTranscriptToBottom() {
    const transcript = transcriptRef.current;
    if (!transcript) return;

    if (scrollFrameRef.current != null) {
      cancelAnimationFrame(scrollFrameRef.current);
    }

    scrollFrameRef.current = requestAnimationFrame(() => {
      scrollFrameRef.current = null;
      transcript.scrollTop = transcript.scrollHeight;
    });
  }

  function clampTranscriptScroll() {
    const transcript = transcriptRef.current;
    if (!transcript) return;

    requestAnimationFrame(() => {
      const maxScrollTop = Math.max(0, transcript.scrollHeight - transcript.clientHeight);
      if (transcript.scrollTop > maxScrollTop) {
        transcript.scrollTop = maxScrollTop;
      }
    });
  }

  function isTranscriptAtBottom(transcript: HTMLDivElement) {
    return transcript.scrollHeight - transcript.scrollTop - transcript.clientHeight <= 4;
  }

  function handleTranscriptScroll() {
    const transcript = transcriptRef.current;
    if (!transcript) return;
    stickToBottomRef.current = isTranscriptAtBottom(transcript);
  }

  function captureTranscriptStickiness() {
    const transcript = transcriptRef.current;
    if (!transcript) return;
    stickToBottomRef.current = isTranscriptAtBottom(transcript);
  }

  function handleTranscriptContentToggled() {
    if (stickToBottomRef.current) {
      scrollTranscriptToBottom();
    } else {
      clampTranscriptScroll();
    }
  }

  return (
    <div className="chat-page">
      <div className="page-header">
        <h2 title={translateTooltip('nav.chat')}>Chat</h2>
        <button
          type="button"
          className="btn-secondary"
          title="Clear the visible conversation and the chat history sent with the next request"
          onClick={clearConversation}
          disabled={!sending && messages.length === 0 && !input.trim()}
        >
          Clear
        </button>
      </div>

      <div className="chat-shell">
        <div className="chat-toolbar">
          <div className="form-group">
            <label title={translateTooltip('chat.database')}>Database</label>
            <select title={translateTooltip('chat.database')} value={databaseId} onChange={event => handleDatabaseChanged(event.target.value)} disabled={sending}>
              {options?.Databases.map(database => (
                <option key={database.Id} value={database.Id}>{database.Name || database.DatabaseName || database.Id}</option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label title={translateTooltip('chat.provider')}>Provider</label>
            <select title={translateTooltip('chat.provider')} value={providerId} onChange={event => handleProviderChanged(event.target.value)} disabled={sending}>
              {options?.Providers.map(provider => (
                <option key={provider.Id} value={provider.Id}>{provider.Name || provider.Id} ({provider.Model})</option>
              ))}
            </select>
          </div>
          <label className="toggle-row" title={translateTooltip('chat.streaming')}>
            <input title={translateTooltip('chat.streaming')} type="checkbox" checked={streaming} onChange={event => setStreaming(event.target.checked)} disabled={sending} />
            <span>Streaming</span>
          </label>
        </div>

        {selectedProvider && (
          <div className="chat-provider-line">
            <span>{selectedProvider.Type}</span>
            <span>{selectedProvider.Endpoint}</span>
            <span>{selectedProvider.HasApiKey ? 'API key configured' : 'No API key'}</span>
            <span>{selectedProvider.UseNativeToolCalls ? 'Native tools' : 'Server fallback'}</span>
          </div>
        )}

        {selectedProvider && !(selectedProvider.UseNativeToolCalls && selectedProvider.SupportsNativeToolCalls) && (
          <div className="chat-capability-notice">
            This provider is not configured for native tool calls. Tablix can use server-side fallback execution for database data requests.
          </div>
        )}

        <div className="chat-transcript" ref={transcriptRef} onScroll={handleTranscriptScroll}>
          <div className="chat-transcript-content" ref={transcriptContentRef}>
            {messages.length === 0 && (
              <div className="chat-empty">
                <h3>Ask about the selected database.</h3>
                <p className="muted-text">Responses can include markdown, SQL, tables, and lists.</p>
              </div>
            )}

            {messages.map(message => (
              <div key={message.Id} className={`chat-message ${message.Role}`}>
                <div className="chat-bubble">
                  {message.Role === 'assistant' && !message.Content
                    ? <AssistantWaitingIndicator />
                    : (
                      <div className="markdown-body">
                        <ReactMarkdown remarkPlugins={[remarkGfm]}>{message.Content}</ReactMarkdown>
                      </div>
                    )
                  }
                  {message.PromptPreview && <PromptPreviewPanel preview={message.PromptPreview} />}
                  {message.ToolCalls.length > 0 && (
                    <ToolCallList
                      toolCalls={message.ToolCalls}
                      onToolCallToggleStart={captureTranscriptStickiness}
                      onToolCallToggled={handleTranscriptContentToggled}
                    />
                  )}
                  {message.Role === 'assistant' && message.VerifiedAnswer && (
                    <VerificationPanel
                      verifiedAnswer={message.VerifiedAnswer}
                      ambiguities={message.Ambiguities}
                      onToggleStart={captureTranscriptStickiness}
                      onToggled={handleTranscriptContentToggled}
                    />
                  )}
                  {message.Role === 'assistant' && (message.ExecutionPath || message.CapabilityNotice) && (
                    <div className="chat-execution-note">
                      {formatExecutionNotes(message).map(note => <span key={note}>{note}</span>)}
                    </div>
                  )}
                  {message.Telemetry && <TelemetryIcon telemetry={message.Telemetry} />}
                </div>
              </div>
            ))}
          </div>
        </div>

        {error && <p className="error-text" style={{ marginTop: '10px' }}>{error}</p>}

        <form className="chat-composer" onSubmit={handleSubmit}>
          <div className="chat-input-stack">
            {slashCommandQuery && filteredSlashCommands.length > 0 && (
              <div className="slash-command-menu" role="listbox" aria-label="Slash commands">
                {filteredSlashCommands.map(command => (
                  <button
                    key={command.Command}
                    type="button"
                    className="slash-command-item"
                    onMouseDown={event => event.preventDefault()}
                    onClick={() => { void runSlashCommand(command.Command); }}
                  >
                    <span>{command.Command}</span>
                    <strong>{command.Label}</strong>
                    <small>{command.Description}</small>
                  </button>
                ))}
              </div>
            )}
            <textarea
              title={translateTooltip('chat.input')}
              value={input}
              onChange={event => setInput(event.target.value)}
              onKeyDown={handleComposerKeyDown}
              placeholder="Ask a question about the selected database, or type / for commands..."
              rows={3}
              disabled={sending || !databaseId || !providerId || !options?.Enabled}
            />
            <span className="chat-input-help">Enter to send, Shift+Enter for newline</span>
          </div>
          <div className="chat-send-column">
            <button
              className={sending ? 'btn-secondary' : 'btn-primary'}
              title={sending ? 'Stop generation' : translateTooltip('chat.send')}
              type={sending ? 'button' : 'submit'}
              onClick={sending ? stopGeneration : undefined}
              disabled={!sending && (!input.trim() || !databaseId || !providerId || !options?.Enabled)}
            >
              {sending ? 'Stop' : 'Send'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function ToolCallList({
  toolCalls,
  onToolCallToggleStart,
  onToolCallToggled,
}: {
  toolCalls: ChatToolCall[];
  onToolCallToggleStart: () => void;
  onToolCallToggled: () => void;
}) {
  return (
    <div className="tool-call-list">
      {toolCalls.map(toolCall => (
        <details
          key={toolCall.Id}
          className={`tool-call ${toolCall.Success ? 'success' : toolCall.Error ? 'failed' : 'running'}`}
          onToggle={onToolCallToggled}
        >
          <summary
            onMouseDown={onToolCallToggleStart}
            onKeyDown={event => {
              if (event.key === 'Enter' || event.key === ' ') onToolCallToggleStart();
            }}
          >
            <span>{toolCall.Name}</span>
            {toolCall.Phase && <span>{toolCall.Phase}</span>}
            <span>{toolCall.Error ? 'Failed' : toolCall.Result ? 'Complete' : 'Running'}</span>
            {toolCall.TotalMs > 0 && <span>{toolCall.TotalMs.toFixed(0)} ms</span>}
          </summary>
          {toolCall.Arguments && (
            <>
              <strong>Arguments</strong>
              <pre>{formatJsonish(toolCall.Arguments)}</pre>
            </>
          )}
          {toolCall.Result && (
            <>
              <strong>Result</strong>
              <pre>{formatJsonish(toolCall.Result)}</pre>
            </>
          )}
          {toolCall.Error && (
            <>
              <strong>Error</strong>
              <pre>{toolCall.Error}</pre>
            </>
          )}
        </details>
      ))}
    </div>
  );
}

function AssistantWaitingIndicator() {
  return (
    <div className="assistant-waiting" aria-label="Waiting for assistant response">
      <span className="assistant-waiting-dot" />
      <span className="assistant-waiting-dot" />
      <span className="assistant-waiting-dot" />
    </div>
  );
}

function VerificationPanel({
  verifiedAnswer,
  ambiguities,
  onToggleStart,
  onToggled,
}: {
  verifiedAnswer: VerifiedAnswer;
  ambiguities: AmbiguitySignal[];
  onToggleStart: () => void;
  onToggled: () => void;
}) {
  return (
    <details className={`verification-panel ${verifiedAnswer.State || 'partial'}`} open onToggle={onToggled}>
      <summary
        className="verification-header"
        onMouseDown={onToggleStart}
        onKeyDown={event => {
          if (event.key === 'Enter' || event.key === ' ') onToggleStart();
        }}
      >
        <span className="verification-state">{formatVerificationState(verifiedAnswer.State)}</span>
        {verifiedAnswer.RowsReturned != null && <span>{verifiedAnswer.RowsReturned} row(s)</span>}
      </summary>
      <div className="verification-body">
        {verifiedAnswer.Summary && <p>{verifiedAnswer.Summary}</p>}
        {verifiedAnswer.Sql && <pre>{verifiedAnswer.Sql}</pre>}
        {verifiedAnswer.Evidence?.length > 0 && (
          <ul>
            {verifiedAnswer.Evidence.slice(0, 4).map(item => <li key={item}>{item}</li>)}
          </ul>
        )}
        {verifiedAnswer.Error && <p className="error-text">{verifiedAnswer.Error}</p>}
        {ambiguities?.length > 0 && (
          <div className="ambiguity-list">
            {ambiguities.slice(0, 3).map(signal => (
              <div key={`${signal.Term}-${signal.Question}`}>
                <strong>{signal.Question || signal.Term}</strong>
                {signal.Candidates.length > 0 && <span>{signal.Candidates.slice(0, 5).join('; ')}</span>}
              </div>
            ))}
          </div>
        )}
      </div>
    </details>
  );
}

function formatVerificationState(value: string | null) {
  if (value === 'verified') return 'Verified';
  if (value === 'blocked') return 'Blocked';
  if (value === 'ambiguous') return 'Ambiguous';
  return 'Partial';
}

function formatJsonish(value: string) {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function isAbortError(value: unknown) {
  return value instanceof DOMException && value.name === 'AbortError';
}

function selectAvailableProviderId(options: ChatOptionsResponse) {
  if (options.DefaultProviderId && options.Providers.some(provider => provider.Id === options.DefaultProviderId)) {
    return options.DefaultProviderId;
  }

  return options.Providers[0]?.Id || '';
}

function getSlashCommandQuery(value: string) {
  const trimmed = value.trimStart();
  if (!trimmed.startsWith('/') || trimmed.includes('\n')) return '';
  return trimmed.split(/\s+/)[0].toLowerCase();
}

function buildSlashHelpMessage() {
  return [
    '### Chat Commands',
    '',
    '| Command | Action |',
    '| --- | --- |',
    ...slashCommands.map(command => `| \`${command.Command}\` | ${command.Description} |`),
    '',
    'Slash commands run locally in the dashboard and are not sent to the model.',
  ].join('\n');
}

function buildUnknownCommandMessage(command: string) {
  return [
    '### Unknown Command',
    '',
    `\`${command || '/'}\` is not a recognized chat command.`,
    '',
    'Use `/help` to show available commands.',
  ].join('\n');
}

function buildContextUsageMarkdown({
  database,
  detail,
  provider,
  settings,
  messages,
  streaming,
  loadWarning,
}: {
  database: ChatOptionsResponse['Databases'][number] | null;
  detail: DatabaseDetail | null;
  provider: ChatOptionsResponse['Providers'][number] | null;
  settings: SettingsReadResponse | null;
  messages: ChatUiMessage[];
  streaming: boolean;
  loadWarning: string;
}) {
  const modelHistory = messages.filter(message => !message.LocalOnly);
  const historyChars = modelHistory.reduce((total, message) => total + message.Content.length, 0);
  const userMessages = modelHistory.filter(message => message.Role === 'user').length;
  const assistantMessages = modelHistory.filter(message => message.Role === 'assistant').length;
  const databaseContext = detail?.Context ?? database?.Context ?? '';
  const databaseContextChars = databaseContext.trim().length;
  const tables = detail?.Tables || [];
  const tableContextChars = tables.reduce((total, table) => total + (table.Context || '').trim().length, 0);
  const tablesWithContext = tables.filter(table => Boolean((table.Context || '').trim())).length;
  const maxContextTables = settings?.Chat.MaxContextTables ?? null;
  const includedTables = maxContextTables == null ? null : Math.min(tables.length, maxContextTables);
  const omittedTables = includedTables == null ? null : Math.max(0, tables.length - includedTables);
  const databaseLabel = database?.Name || database?.DatabaseName || detail?.DatabaseName || database?.Filename || database?.Id || 'None';
  const databaseValue = database?.Id
    ? `${markdownLink(databaseLabel, `/databases/${encodeURIComponent(database.Id)}`)} (${inlineCode(database.Id)})`
    : plainTableCell(databaseLabel);
  const providerValue = provider
    ? `${markdownLink(provider.Name || provider.Id, `/models?provider=${encodeURIComponent(provider.Id)}`)} (${plainTableCell(provider.Model || provider.Type)})`
    : 'None';
  const rows: Array<[string, string]> = [
    ['Database', databaseValue],
    ['Provider', providerValue],
    ['Streaming', streaming ? 'On' : 'Off'],
    ['Conversation history messages', `${formatNumber(modelHistory.length)} (${formatNumber(userMessages)} user, ${formatNumber(assistantMessages)} assistant)`],
    ['Conversation history characters', formatNumber(historyChars)],
    ['Conversation history token estimate', formatNumber(estimateTokens(historyChars))],
    ['Saved database context', databaseContextChars > 0 ? 'Present' : 'None'],
    ['Saved database context characters', formatNumber(databaseContextChars)],
    ['Saved database context token estimate', formatNumber(estimateTokens(databaseContextChars))],
  ];

  const lines = ['### Context Usage', ''];
  if (loadWarning) {
    lines.push(`> ${loadWarning}`, '');
  }

  if (detail) {
    rows.push(
      ['Crawl state', `${detail.IsCrawled ? 'Crawled' : 'Degraded'}${detail.CrawlError ? ` (${plainTableCell(detail.CrawlError)})` : ''}`],
      ['Schema tables available', formatNumber(tables.length)]
    );

    if (maxContextTables != null && includedTables != null && omittedTables != null) {
      rows.push(
        ['Prompt table limit', formatNumber(maxContextTables)],
        ['Prompt tables included', formatNumber(includedTables)],
        ['Prompt tables omitted', formatNumber(omittedTables)]
      );
    }

    rows.push(
      ['Saved table context tables', `${formatNumber(tablesWithContext)} of ${formatNumber(tables.length)}`],
      ['Saved table context characters', formatNumber(tableContextChars)],
      ['Saved table context token estimate', formatNumber(estimateTokens(tableContextChars))]
    );
  } else {
    rows.push(['Crawl state', database?.IsCrawled ? 'Crawled' : 'Degraded or unavailable']);
  }

  if (settings) {
    rows.push(
      ['Query tools', settings.Chat.Tools.Enabled ? 'Enabled' : 'Disabled'],
      ['Read-only execution', settings.Chat.Tools.AllowReadOnlyQueries ? 'Allowed' : 'Disabled'],
      ['Context update tools', settings.Chat.Tools.AllowContextUpdates ? 'Enabled' : 'Disabled']
    );
  }

  if (provider) {
    rows.push(['Tool mode', provider.UseNativeToolCalls && provider.SupportsNativeToolCalls ? 'Native tool calls' : 'Server fallback/plain chat depending on request and settings']);
  }

  lines.push('| Key | Value |', '| --- | --- |', ...rows.map(([key, value]) => markdownTableRow(key, value)));
  lines.push('', '`/clear` removes the visible conversation and the model-bound history used for the next request.');
  return lines.join('\n');
}

function buildPromptPreviewMarkdown(preview: ChatPromptPreviewResponse) {
  const rows: Array<[string, string]> = [
    ['Database', inlineCode(preview.DatabaseId || 'n/a')],
    ['Provider', inlineCode(preview.ProviderId || 'n/a')],
    ['Model', plainTableCell(preview.Model || 'n/a')],
    ['Conversation messages included', formatNumber(preview.ConversationMessages)],
    ['System prompt characters', formatNumber(preview.SystemPromptCharacters)],
    ['System prompt token estimate', formatNumber(preview.SystemPromptEstimatedTokens)],
    ['Database context prompt characters', formatNumber(preview.ContextPromptCharacters)],
    ['Database context prompt token estimate', formatNumber(preview.ContextPromptEstimatedTokens)],
  ];

  return [
    '### Prompt Preview',
    '',
    '| Key | Value |',
    '| --- | --- |',
    ...rows.map(([key, value]) => markdownTableRow(key, value)),
  ].join('\n');
}

function PromptPreviewPanel({ preview }: { preview: ChatPromptPreviewResponse }) {
  const fields = [
    {
      Key: 'system',
      Label: 'Effective System Prompt',
      Text: preview.SystemPrompt || '',
      EmptyText: '(empty)',
      CopyTitle: 'Copy effective system prompt',
      CopyLabel: 'Copy effective system prompt',
    },
    {
      Key: 'context',
      Label: 'Database Context Prompt',
      Text: preview.ContextPrompt || '',
      EmptyText: '(empty)',
      CopyTitle: 'Copy database context prompt',
      CopyLabel: 'Copy database context prompt',
    },
  ];

  return (
    <div className="prompt-preview-fields">
      {fields.map(field => (
        <section className="prompt-preview-field" key={field.Key}>
          <div className="prompt-preview-field-header">
            <h4>{field.Label}</h4>
            <ClipboardButton text={field.Text} title={field.CopyTitle} label={field.CopyLabel} />
          </div>
          <pre><code>{field.Text || field.EmptyText}</code></pre>
        </section>
      ))}
    </div>
  );
}

function markdownTableRow(key: string, value: string) {
  return `| ${plainTableCell(key)} | ${value || 'n/a'} |`;
}

function markdownLink(label: string, href: string) {
  return `[${escapeMarkdownLinkText(label)}](${href})`;
}

function inlineCode(value: string) {
  return '`' + String(value).replace(/`/g, '\\`') + '`';
}

function plainTableCell(value: string | number | null) {
  if (value == null) return 'n/a';
  return String(value).replace(/\|/g, '\\|').replace(/\r?\n/g, ' ');
}

function escapeMarkdownLinkText(value: string) {
  return String(value).replace(/\\/g, '\\\\').replace(/\[/g, '\\[').replace(/\]/g, '\\]');
}

function estimateTokens(characters: number) {
  if (!characters || characters <= 0) return 0;
  return Math.max(1, Math.ceil(characters / 4));
}

function formatExecutionPath(value: string) {
  if (value === 'native_tool_calls') return 'A database tool was used to answer this message.';
  if (value === 'server_fallback') return 'Tablix ran a database query to answer this message.';
  if (value === 'plain') return 'The assistant answered without running a database query.';
  if (value === 'execution_disabled') return 'Database query execution is disabled for this chat.';
  if (value === 'native_failed_plain') return 'Tool execution failed; the assistant returned a plain model response.';
  if (value === 'native_tool_call_failed') return 'The requested database tool call failed.';
  if (value === 'native_no_response') return 'The model did not return a usable response.';
  if (value === 'native_final_failed') return 'The final answer after tool execution failed.';
  if (value === 'fallback_planner_failed') return 'Tablix could not plan a database query for this request.';
  if (value === 'fallback_no_plan') return 'No database query was run for this request.';
  if (value === 'ambiguity_check') return 'Tablix asked for clarification before running SQL.';
  return 'Execution status: ' + value.replace(/_/g, ' ');
}

function formatExecutionNotes(message: ChatUiMessage) {
  if (message.ExecutionPath === 'native_no_tool_call') {
    return ['The model did not request a tool call.'];
  }

  const notes: string[] = [];
  if (message.ExecutionPath) {
    notes.push(formatExecutionPath(message.ExecutionPath));
  }

  const capabilityNotice = formatCapabilityNotice(message.CapabilityNotice);
  if (capabilityNotice) {
    notes.push(capabilityNotice);
  }

  return notes;
}

function formatCapabilityNotice(value: string | null) {
  if (!value) return null;
  if (value.includes('Native tool calls are enabled for this provider')) return null;
  if (value.includes('The model did not request a native tool for this data question')) {
    return 'The model did not request a database tool, so Tablix used server-side query execution.';
  }

  return value;
}

function TelemetryIcon({ telemetry }: { telemetry: ChatTelemetry }) {
  return (
    <span className="telemetry-icon" tabIndex={0} aria-label="Message telemetry">
      i
      <span className="telemetry-popover">
        <strong>Telemetry</strong>
        <span>TTFT: {formatMs(telemetry.TimeToFirstTokenMs)}</span>
        <span>Total: {formatMs(telemetry.TotalStreamingTimeMs)}</span>
        <span>Input tokens: {formatNumber(telemetry.InputTokens)}</span>
        <span>Output tokens: {formatNumber(telemetry.OutputTokens)}</span>
        <span>Total tokens: {formatNumber(telemetry.TotalTokens)}{telemetry.EstimatedTokens ? ' est.' : ''}</span>
      </span>
    </span>
  );
}

function formatMs(value: number | null) {
  if (value == null) return 'n/a';
  return `${Math.round(value)} ms`;
}

function formatNumber(value: number | null) {
  if (value == null) return 'n/a';
  return value.toLocaleString();
}
