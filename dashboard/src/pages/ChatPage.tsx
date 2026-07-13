import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { useNavigate } from 'react-router-dom';
import { apiFetch } from '../api/client';
import { translateTooltip } from '../i18n';
import type { ChatMessageRequest, ChatOptionsResponse, ChatRequest, ChatResponseResult, ChatStreamEvent, ChatTelemetry, ChatToolCall } from '../types';

interface ChatUiMessage {
  Id: string;
  Role: 'user' | 'assistant';
  Content: string;
  Telemetry: ChatTelemetry | null;
  ToolCalls: ChatToolCall[];
  ExecutionPath: string | null;
  CapabilityNotice: string | null;
}

interface ParsedSseFrame {
  event: string;
  data: string;
}

export default function ChatPage() {
  const navigate = useNavigate();
  const transcriptRef = useRef<HTMLDivElement | null>(null);
  const transcriptContentRef = useRef<HTMLDivElement | null>(null);
  const stickToBottomRef = useRef(true);
  const scrollFrameRef = useRef<number | null>(null);
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
    };
  }, []);

  const selectedProvider = useMemo(
    () => options?.Providers.find(provider => provider.Id === providerId) || null,
    [options, providerId]
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

    const userMessage: ChatUiMessage = {
      Id: crypto.randomUUID(),
      Role: 'user',
      Content: trimmed,
      Telemetry: null,
      ToolCalls: [],
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
      ExecutionPath: null,
      CapabilityNotice: null,
    };

    const nextMessages = [...messages, userMessage, assistantMessage];
    setMessages(nextMessages);
    setInput('');
    setSending(true);
    setError('');

    const request: ChatRequest = {
      DatabaseId: databaseId,
      ProviderId: providerId,
      Messages: [...messages, userMessage].map(toRequestMessage),
      Streaming: streaming,
    };

    if (streaming) {
      await sendStreaming(request, assistantId);
    } else {
      await sendNonStreaming(request, assistantId);
    }

    setSending(false);
  }

  async function sendNonStreaming(request: ChatRequest, assistantId: string) {
    try {
      const response = await apiFetch('/v1/chat', {
        method: 'POST',
        body: JSON.stringify(request),
      });

      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) {
        const message = await response.text();
        failAssistant(assistantId, message || 'Chat request failed.');
        return;
      }

      const result: ChatResponseResult = await response.json();
      setMessages(previous => previous.map(message => message.Id === assistantId
        ? { ...message, Content: result.Message || '', Telemetry: result.Telemetry, ToolCalls: result.ToolCalls || [], ExecutionPath: result.ExecutionPath, CapabilityNotice: result.CapabilityNotice }
        : message));
    } catch {
      failAssistant(assistantId, 'Could not connect to server.');
    }
  }

  async function sendStreaming(request: ChatRequest, assistantId: string) {
    try {
      const response = await apiFetch('/v1/chat/stream', {
        method: 'POST',
        body: JSON.stringify(request),
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
    } catch {
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
        ? { ...message, Content: event.Message || message.Content, Telemetry: event.Telemetry, ExecutionPath: event.ExecutionPath || message.ExecutionPath, CapabilityNotice: event.CapabilityNotice || message.CapabilityNotice }
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
                  {message.ToolCalls.length > 0 && (
                    <ToolCallList
                      toolCalls={message.ToolCalls}
                      onToolCallToggleStart={captureTranscriptStickiness}
                      onToolCallToggled={handleTranscriptContentToggled}
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
          <textarea
            title={translateTooltip('chat.input')}
            value={input}
            onChange={event => setInput(event.target.value)}
            onKeyDown={handleComposerKeyDown}
            placeholder="Ask a question about the selected database..."
            rows={3}
            disabled={sending || !databaseId || !providerId || !options?.Enabled}
          />
            <span className="chat-input-help">Enter to send, Shift+Enter for newline</span>
          </div>
          <div className="chat-send-column">
            <button className="btn-primary" title={translateTooltip('chat.send')} type="submit" disabled={sending || !input.trim() || !databaseId || !providerId || !options?.Enabled}>
              {sending ? 'Sending...' : 'Send'}
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

function formatJsonish(value: string) {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function selectAvailableProviderId(options: ChatOptionsResponse) {
  if (options.DefaultProviderId && options.Providers.some(provider => provider.Id === options.DefaultProviderId)) {
    return options.DefaultProviderId;
  }

  return options.Providers[0]?.Id || '';
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
