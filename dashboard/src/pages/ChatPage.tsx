import { useEffect, useMemo, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { useNavigate } from 'react-router-dom';
import { apiFetch } from '../api/client';
import type { ChatMessageRequest, ChatOptionsResponse, ChatRequest, ChatResponseResult, ChatStreamEvent, ChatTelemetry, ChatToolCall } from '../types';

interface ChatUiMessage {
  Id: string;
  Role: 'user' | 'assistant';
  Content: string;
  Telemetry: ChatTelemetry | null;
  ToolCalls: ChatToolCall[];
}

interface ParsedSseFrame {
  event: string;
  data: string;
}

export default function ChatPage() {
  const navigate = useNavigate();
  const transcriptRef = useRef<HTMLDivElement | null>(null);
  const [options, setOptions] = useState<ChatOptionsResponse | null>(null);
  const [databaseId, setDatabaseId] = useState('');
  const [providerId, setProviderId] = useState('');
  const [streaming, setStreaming] = useState(true);
  const [messages, setMessages] = useState<ChatUiMessage[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => { loadOptions(); }, []);
  useEffect(() => { scrollTranscriptToBottom(); }, [messages]);

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
      setProviderId(data.DefaultProviderId || data.Providers[0]?.Id || '');
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
    };
    const assistantId = crypto.randomUUID();
    const assistantMessage: ChatUiMessage = {
      Id: assistantId,
      Role: 'assistant',
      Content: '',
      Telemetry: null,
      ToolCalls: [],
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
        ? { ...message, Content: result.Message || '', Telemetry: result.Telemetry, ToolCalls: result.ToolCalls || [] }
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
        ? { ...message, Content: event.Message || message.Content, Telemetry: event.Telemetry }
        : message));
    } else if ((event.EventType === 'tool_started' || event.EventType === 'tool_completed') && event.ToolCall) {
      setMessages(previous => previous.map(message => message.Id === assistantId
        ? { ...message, ToolCalls: mergeToolCall(message.ToolCalls, event.ToolCall as ChatToolCall) }
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

  function toRequestMessage(message: ChatUiMessage): ChatMessageRequest {
    return {
      Role: message.Role,
      Content: message.Content,
    };
  }

  function scrollTranscriptToBottom() {
    const transcript = transcriptRef.current;
    if (!transcript) return;

    transcript.scrollTo({
      top: transcript.scrollHeight,
      behavior: 'smooth',
    });
  }

  return (
    <div className="chat-page">
      <div className="page-header">
        <h2 title="Chat with a configured database">Chat</h2>
      </div>

      <div className="chat-shell">
        <div className="chat-toolbar">
          <div className="form-group">
            <label title="Database used for schema and context">Database</label>
            <select value={databaseId} onChange={event => setDatabaseId(event.target.value)} disabled={sending}>
              {options?.Databases.map(database => (
                <option key={database.Id} value={database.Id}>{database.Name || database.DatabaseName || database.Id}</option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label title="Configured model endpoint">Provider</label>
            <select value={providerId} onChange={event => setProviderId(event.target.value)} disabled={sending}>
              {options?.Providers.map(provider => (
                <option key={provider.Id} value={provider.Id}>{provider.Name || provider.Id} ({provider.Model})</option>
              ))}
            </select>
          </div>
          <label className="toggle-row" title="Stream model tokens as they arrive">
            <input type="checkbox" checked={streaming} onChange={event => setStreaming(event.target.checked)} disabled={sending} />
            <span>Streaming</span>
          </label>
        </div>

        {selectedProvider && (
          <div className="chat-provider-line">
            <span>{selectedProvider.Type}</span>
            <span>{selectedProvider.Endpoint}</span>
            <span>{selectedProvider.HasApiKey ? 'API key configured' : 'No API key'}</span>
          </div>
        )}

        <div className="chat-transcript" ref={transcriptRef}>
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
                {message.ToolCalls.length > 0 && <ToolCallList toolCalls={message.ToolCalls} />}
                {message.Telemetry && <TelemetryIcon telemetry={message.Telemetry} />}
              </div>
            </div>
          ))}
        </div>

        {error && <p className="error-text" style={{ marginTop: '10px' }}>{error}</p>}

        <form className="chat-composer" onSubmit={handleSubmit}>
          <div className="chat-input-stack">
          <textarea
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
            <button className="btn-primary" type="submit" disabled={sending || !input.trim() || !databaseId || !providerId || !options?.Enabled}>
              {sending ? 'Sending...' : 'Send'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function ToolCallList({ toolCalls }: { toolCalls: ChatToolCall[] }) {
  return (
    <div className="tool-call-list">
      {toolCalls.map(toolCall => (
        <details key={toolCall.Id} className={`tool-call ${toolCall.Success ? 'success' : toolCall.Error ? 'failed' : 'running'}`}>
          <summary>
            <span>{toolCall.Name}</span>
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
