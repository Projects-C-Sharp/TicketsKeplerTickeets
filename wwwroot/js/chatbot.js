// ─── Kepler Chatbot ──────────────────────────────────────────────────────────
// Powered by OpenAI GPT-4o-mini
// Strictly scoped to Kepler Tickets app context

(function () {
  'use strict';

  // ── Configuration ────────────────────────────────────────────────────────
  const API_ENDPOINT = '/Chatbot/Message';
  const MAX_MESSAGES = 40; // keep context manageable
  const BOT_NAME = 'Kepler AI';

  // ── State ────────────────────────────────────────────────────────────────
  let isOpen = false;
  let isLoading = false;
  let conversationHistory = []; // { role: 'user'|'assistant', content: string }

  // ── DOM Creation ─────────────────────────────────────────────────────────
  function createWidget() {
    const widget = document.createElement('div');
    widget.id = 'kepler-chat';
    widget.innerHTML = `
      <button class="kc-toggle" id="kcToggle" aria-label="Abrir asistente">
        <span class="kc-toggle-icon kc-icon-chat">◈</span>
        <span class="kc-toggle-icon kc-icon-close" style="display:none">✕</span>
        <span class="kc-badge" id="kcBadge" style="display:none">1</span>
      </button>
      <div class="kc-panel" id="kcPanel" aria-hidden="true">
        <div class="kc-header">
          <div class="kc-header-info">
            <div class="kc-avatar">◈</div>
            <div>
              <div class="kc-header-name">${BOT_NAME}</div>
              <div class="kc-header-status">
                <span class="kc-status-dot"></span>En línea
              </div>
            </div>
          </div>
          <button class="kc-close-btn" id="kcClose" aria-label="Cerrar">✕</button>
        </div>
        <div class="kc-messages" id="kcMessages">
          <div class="kc-welcome">
            <div class="kc-welcome-icon">🎟️</div>
            <p class="kc-welcome-title">¡Hola! Soy el asistente de <strong>Kepler Tickets</strong>.</p>
            <p class="kc-welcome-sub">Puedo ayudarte con eventos, tickets, reservas y tu cuenta. ¿En qué te ayudo?</p>
            <div class="kc-suggestions">
              <button class="kc-suggestion" data-msg="¿Cómo compro un ticket?">¿Cómo compro un ticket?</button>
              <button class="kc-suggestion" data-msg="¿Dónde veo mis órdenes?">¿Dónde veo mis órdenes?</button>
              <button class="kc-suggestion" data-msg="¿Cómo funciona la reserva de asientos?">¿Cómo funciona la reserva?</button>
              <button class="kc-suggestion" data-msg="¿Qué tipos de asientos hay?">Tipos de asientos</button>
            </div>
          </div>
        </div>
        <div class="kc-input-area">
          <div class="kc-input-wrap">
            <textarea
              id="kcInput"
              class="kc-input"
              placeholder="Escribe tu pregunta..."
              rows="1"
              maxlength="500"
              aria-label="Mensaje"
            ></textarea>
            <button class="kc-send" id="kcSend" aria-label="Enviar" disabled>
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                <line x1="22" y1="2" x2="11" y2="13"></line>
                <polygon points="22 2 15 22 11 13 2 9 22 2"></polygon>
              </svg>
            </button>
          </div>
          <p class="kc-disclaimer">Solo respondo sobre Kepler Tickets · <a href="/Events" style="color:var(--c-yellow)">Ver eventos →</a></p>
        </div>
      </div>
    `;
    document.body.appendChild(widget);
  }

  // ── Message Rendering ────────────────────────────────────────────────────
  function renderMessage(role, text, isStreaming) {
    const container = document.getElementById('kcMessages');
    const msgEl = document.createElement('div');
    msgEl.className = `kc-msg kc-msg--${role}`;
    if (isStreaming) msgEl.id = 'kcStreamingMsg';

    const bubble = document.createElement('div');
    bubble.className = 'kc-bubble';
    bubble.innerHTML = formatText(text);

    if (role === 'assistant') {
      const avatar = document.createElement('div');
      avatar.className = 'kc-msg-avatar';
      avatar.textContent = '◈';
      msgEl.appendChild(avatar);
    }

    msgEl.appendChild(bubble);
    container.appendChild(msgEl);
    container.scrollTop = container.scrollHeight;
    return msgEl;
  }

  function formatText(text) {
    // Basic markdown-like formatting
    return text
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.*?)\*/g, '<em>$1</em>')
      .replace(/`(.*?)`/g, '<code>$1</code>')
      .replace(/\n/g, '<br>');
  }

  function renderTypingIndicator() {
    const container = document.getElementById('kcMessages');
    const el = document.createElement('div');
    el.className = 'kc-msg kc-msg--assistant';
    el.id = 'kcTyping';
    el.innerHTML = `
      <div class="kc-msg-avatar">◈</div>
      <div class="kc-bubble kc-bubble--typing">
        <span></span><span></span><span></span>
      </div>
    `;
    container.appendChild(el);
    container.scrollTop = container.scrollHeight;
  }

  function removeTypingIndicator() {
    const el = document.getElementById('kcTyping');
    if (el) el.remove();
  }

  // ── API Call ─────────────────────────────────────────────────────────────
  async function sendMessage(userText) {
    if (isLoading || !userText.trim()) return;

    isLoading = true;
    setInputState(false);

    // Add user message to UI and history
    renderMessage('user', userText);
    conversationHistory.push({ role: 'user', content: userText });

    // Trim history if too long
    if (conversationHistory.length > MAX_MESSAGES) {
      conversationHistory = conversationHistory.slice(-MAX_MESSAGES);
    }

    renderTypingIndicator();

    try {
      const response = await fetch(API_ENDPOINT, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ history: conversationHistory }),
        credentials: 'same-origin'
      });

      removeTypingIndicator();

      if (!response.ok) {
        throw new Error('Error de servidor: ' + response.status);
      }

      const data = await response.json();
      const reply = data.reply || 'Lo siento, no pude procesar tu mensaje.';

      renderMessage('assistant', reply);
      conversationHistory.push({ role: 'assistant', content: reply });

    } catch (err) {
      removeTypingIndicator();
      renderMessage('assistant', 'Hubo un problema conectándome. Por favor intenta de nuevo en un momento.');
      console.error('Chatbot error:', err);
    } finally {
      isLoading = false;
      setInputState(true);
      document.getElementById('kcInput')?.focus();
    }
  }

  // ── UI Helpers ───────────────────────────────────────────────────────────
  function setInputState(enabled) {
    const input = document.getElementById('kcInput');
    const send = document.getElementById('kcSend');
    if (input) input.disabled = !enabled;
    if (send) {
      const hasText = input && input.value.trim().length > 0;
      send.disabled = !enabled || !hasText;
    }
  }

  function togglePanel() {
    isOpen = !isOpen;
    const panel = document.getElementById('kcPanel');
    const iconChat = document.querySelector('.kc-icon-chat');
    const iconClose = document.querySelector('.kc-icon-close');
    const badge = document.getElementById('kcBadge');

    panel.setAttribute('aria-hidden', !isOpen);
    panel.classList.toggle('is-open', isOpen);
    iconChat.style.display = isOpen ? 'none' : '';
    iconClose.style.display = isOpen ? '' : 'none';
    if (badge) badge.style.display = 'none';

    if (isOpen) {
      setTimeout(() => document.getElementById('kcInput')?.focus(), 150);
    }
  }

  function autoResizeInput(el) {
    el.style.height = 'auto';
    el.style.height = Math.min(el.scrollHeight, 120) + 'px';
  }

  // ── Event Binding ────────────────────────────────────────────────────────
  function bindEvents() {
    const toggle = document.getElementById('kcToggle');
    const closeBtn = document.getElementById('kcClose');
    const input = document.getElementById('kcInput');
    const send = document.getElementById('kcSend');
    const messages = document.getElementById('kcMessages');

    toggle.addEventListener('click', togglePanel);
    closeBtn.addEventListener('click', togglePanel);

    input.addEventListener('input', function () {
      autoResizeInput(this);
      const send = document.getElementById('kcSend');
      if (send) send.disabled = isLoading || !this.value.trim();
    });

    input.addEventListener('keydown', function (e) {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        handleSend();
      }
    });

    send.addEventListener('click', handleSend);

    // Suggestion chips
    messages.addEventListener('click', function (e) {
      const chip = e.target.closest('.kc-suggestion');
      if (chip) {
        const msg = chip.dataset.msg;
        // Remove welcome block
        const welcome = document.querySelector('.kc-welcome');
        if (welcome) welcome.remove();
        sendMessage(msg);
      }
    });
  }

  function handleSend() {
    const input = document.getElementById('kcInput');
    if (!input) return;
    const text = input.value.trim();
    if (!text || isLoading) return;

    // Remove welcome block on first real message
    const welcome = document.querySelector('.kc-welcome');
    if (welcome) welcome.remove();

    input.value = '';
    input.style.height = 'auto';
    document.getElementById('kcSend').disabled = true;
    sendMessage(text);
  }

  // ── Styles ───────────────────────────────────────────────────────────────
  function injectStyles() {
    const style = document.createElement('style');
    style.textContent = `
      /* ─── Chatbot Widget ─────────────────────────────────────────── */
      #kepler-chat {
        position: fixed;
        bottom: 1.5rem;
        right: 1.5rem;
        z-index: 9000;
        font-family: 'DM Sans', sans-serif;
      }

      /* Toggle Button */
      .kc-toggle {
        position: relative;
        width: 56px;
        height: 56px;
        border-radius: 50%;
        background: var(--c-yellow, #F5FF00);
        color: var(--c-dark, #0D0D1F);
        border: none;
        cursor: pointer;
        font-size: 1.4rem;
        font-weight: 800;
        box-shadow: 0 4px 24px rgba(245,255,0,.35);
        transition: transform .2s ease, box-shadow .2s ease;
        display: flex;
        align-items: center;
        justify-content: center;
        margin-left: auto;
      }
      .kc-toggle:hover {
        transform: scale(1.08);
        box-shadow: 0 6px 32px rgba(245,255,0,.5);
      }
      .kc-toggle:active { transform: scale(.96); }

      .kc-badge {
        position: absolute;
        top: -4px;
        right: -4px;
        width: 18px;
        height: 18px;
        background: var(--c-pink, #FF006E);
        color: #fff;
        border-radius: 50%;
        font-size: .65rem;
        font-weight: 700;
        display: flex;
        align-items: center;
        justify-content: center;
        border: 2px solid var(--c-dark, #0D0D1F);
      }

      /* Panel */
      .kc-panel {
        position: absolute;
        bottom: calc(100% + 12px);
        right: 0;
        width: 360px;
        max-height: 540px;
        background: var(--c-surface, #12122a);
        border: 1px solid var(--c-border, #1e1e3a);
        border-radius: 20px;
        box-shadow: 0 16px 64px rgba(0,0,0,.7);
        display: flex;
        flex-direction: column;
        overflow: hidden;
        opacity: 0;
        transform: translateY(12px) scale(.97);
        pointer-events: none;
        transition: opacity .2s ease, transform .2s ease;
      }
      .kc-panel.is-open {
        opacity: 1;
        transform: translateY(0) scale(1);
        pointer-events: all;
      }

      /* Header */
      .kc-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: .9rem 1rem;
        border-bottom: 1px solid var(--c-border, #1e1e3a);
        background: var(--c-surface2, #171730);
        flex-shrink: 0;
      }
      .kc-header-info { display: flex; align-items: center; gap: .7rem; }
      .kc-avatar {
        width: 36px; height: 36px;
        background: var(--c-yellow, #F5FF00);
        color: var(--c-dark, #0D0D1F);
        border-radius: 50%;
        display: flex; align-items: center; justify-content: center;
        font-size: 1rem; font-weight: 800; flex-shrink: 0;
      }
      .kc-header-name { font-family: 'Syne', sans-serif; font-weight: 700; font-size: .9rem; color: var(--c-text, #f0f0ff); }
      .kc-header-status { display: flex; align-items: center; gap: .35rem; font-size: .72rem; color: var(--c-muted, #8888aa); }
      .kc-status-dot { width: 7px; height: 7px; background: var(--c-green, #34d399); border-radius: 50%; animation: kcPulse 2s infinite; }

      @keyframes kcPulse {
        0%, 100% { opacity: 1; }
        50% { opacity: .4; }
      }

      .kc-close-btn {
        background: transparent; border: none; cursor: pointer;
        color: var(--c-muted, #8888aa); font-size: 1rem; padding: .2rem;
        line-height: 1; transition: color .15s;
      }
      .kc-close-btn:hover { color: var(--c-text, #f0f0ff); }

      /* Messages */
      .kc-messages {
        flex: 1;
        overflow-y: auto;
        padding: 1rem;
        display: flex;
        flex-direction: column;
        gap: .75rem;
        scrollbar-width: thin;
        scrollbar-color: var(--c-border, #1e1e3a) transparent;
      }
      .kc-messages::-webkit-scrollbar { width: 4px; }
      .kc-messages::-webkit-scrollbar-thumb { background: var(--c-border, #1e1e3a); border-radius: 4px; }

      /* Welcome */
      .kc-welcome { text-align: center; padding: .5rem 0 .25rem; }
      .kc-welcome-icon { font-size: 2rem; margin-bottom: .5rem; }
      .kc-welcome-title { font-size: .88rem; color: var(--c-text, #f0f0ff); margin-bottom: .3rem; }
      .kc-welcome-sub { font-size: .78rem; color: var(--c-muted, #8888aa); margin-bottom: .85rem; line-height: 1.5; }
      .kc-suggestions { display: flex; flex-wrap: wrap; gap: .4rem; justify-content: center; }
      .kc-suggestion {
        padding: .35rem .75rem;
        background: rgba(245,255,0,.08);
        border: 1px solid rgba(245,255,0,.2);
        border-radius: 99px;
        color: var(--c-yellow, #F5FF00);
        font-size: .75rem;
        font-family: 'DM Sans', sans-serif;
        cursor: pointer;
        transition: all .15s;
      }
      .kc-suggestion:hover { background: rgba(245,255,0,.15); }

      /* Message bubbles */
      .kc-msg {
        display: flex;
        align-items: flex-end;
        gap: .5rem;
        max-width: 88%;
      }
      .kc-msg--user { align-self: flex-end; flex-direction: row-reverse; }
      .kc-msg--assistant { align-self: flex-start; }

      .kc-msg-avatar {
        width: 26px; height: 26px;
        background: var(--c-yellow, #F5FF00);
        color: var(--c-dark, #0D0D1F);
        border-radius: 50%;
        display: flex; align-items: center; justify-content: center;
        font-size: .7rem; font-weight: 800; flex-shrink: 0;
      }

      .kc-bubble {
        padding: .6rem .85rem;
        border-radius: 16px;
        font-size: .82rem;
        line-height: 1.55;
        word-break: break-word;
      }
      .kc-msg--user .kc-bubble {
        background: var(--c-yellow, #F5FF00);
        color: var(--c-dark, #0D0D1F);
        border-bottom-right-radius: 4px;
        font-weight: 500;
      }
      .kc-msg--assistant .kc-bubble {
        background: var(--c-surface2, #171730);
        color: var(--c-text, #f0f0ff);
        border: 1px solid var(--c-border, #1e1e3a);
        border-bottom-left-radius: 4px;
      }
      .kc-bubble code {
        background: rgba(245,255,0,.12);
        color: var(--c-yellow, #F5FF00);
        border-radius: 4px;
        padding: .1rem .3rem;
        font-size: .8em;
        font-family: monospace;
      }

      /* Typing indicator */
      .kc-bubble--typing {
        display: flex; gap: 5px; align-items: center;
        padding: .75rem .85rem;
      }
      .kc-bubble--typing span {
        width: 7px; height: 7px;
        background: var(--c-muted, #8888aa);
        border-radius: 50%;
        animation: kcBounce .9s infinite;
      }
      .kc-bubble--typing span:nth-child(2) { animation-delay: .15s; }
      .kc-bubble--typing span:nth-child(3) { animation-delay: .3s; }

      @keyframes kcBounce {
        0%, 60%, 100% { transform: translateY(0); opacity: .5; }
        30% { transform: translateY(-5px); opacity: 1; }
      }

      /* Input area */
      .kc-input-area {
        padding: .75rem;
        border-top: 1px solid var(--c-border, #1e1e3a);
        background: var(--c-surface2, #171730);
        flex-shrink: 0;
      }
      .kc-input-wrap {
        display: flex;
        align-items: flex-end;
        gap: .5rem;
        background: var(--c-bg, #0D0D1F);
        border: 1px solid var(--c-border, #1e1e3a);
        border-radius: var(--radius, 12px);
        padding: .5rem .5rem .5rem .8rem;
        transition: border-color .15s;
      }
      .kc-input-wrap:focus-within { border-color: rgba(245,255,0,.4); }

      .kc-input {
        flex: 1;
        background: transparent;
        border: none;
        outline: none;
        color: var(--c-text, #f0f0ff);
        font-family: 'DM Sans', sans-serif;
        font-size: .83rem;
        line-height: 1.4;
        resize: none;
        max-height: 120px;
        overflow-y: auto;
        scrollbar-width: none;
      }
      .kc-input::placeholder { color: var(--c-muted, #8888aa); }
      .kc-input:disabled { opacity: .6; }

      .kc-send {
        width: 32px; height: 32px; border-radius: 8px; flex-shrink: 0;
        background: var(--c-yellow, #F5FF00);
        color: var(--c-dark, #0D0D1F);
        display: flex; align-items: center; justify-content: center;
        transition: all .15s;
        padding: 0;
      }
      .kc-send svg { width: 14px; height: 14px; }
      .kc-send:hover:not(:disabled) { background: #e8f200; }
      .kc-send:disabled { opacity: .35; cursor: not-allowed; }

      .kc-disclaimer {
        text-align: center;
        font-size: .68rem;
        color: var(--c-muted, #8888aa);
        margin-top: .4rem;
      }

      /* Mobile adjustments */
      @media (max-width: 420px) {
        #kepler-chat { bottom: 1rem; right: 1rem; }
        .kc-panel { width: calc(100vw - 2rem); right: -1rem; }
      }
    `;
    document.head.appendChild(style);
  }

  // ── Init ─────────────────────────────────────────────────────────────────
  function init() {
    injectStyles();
    createWidget();
    bindEvents();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

})();
