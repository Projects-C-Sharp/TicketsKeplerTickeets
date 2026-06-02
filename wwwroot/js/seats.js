/**
 * seats.js — Seat selection, reservation, order & payment flow
 *
 * Flow:
 *   1. User selects seats → clicks "Reservar"
 *   2. POST /Orders/Reserve  → seats locked, timer starts (NO order yet)
 *   3. User clicks "Pagar"   → POST /Orders/Create, then POST /Orders/Pay
 *   4. Cancel reservation    → POST /Orders/Release  (no order to cancel)
 *   5. Cancel pending order  → POST /Orders/CancelReservation (release seats)
 *                              + POST /Orders/CancelOrder      (cancel the order)
 */
(function () {
  'use strict';

  /* ── State ─────────────────────────────────────────────────────── */
  let selectedSeats   = [];
  let reservedSeatIds = [];
  let currentOrderId  = null;
  let timerInterval   = null;
  let pollInterval    = null;
  let reserveExpiry   = null;
  const seatStatus = {};
  SEATS_DATA.forEach(s => { seatStatus[s.id] = s.status; });

  const MAX_SEATS = 8;
  const POLL_MS   = 7000;

  // Pending-order resume mode
  const hasPendingOrder = typeof PENDING_ORDER !== 'undefined' && PENDING_ORDER !== null;

  /* ── DOM refs ───────────────────────────────────────────────────── */
  const grid            = document.getElementById('seatGrid');
  const selectedList    = document.getElementById('selectedList');
  const checkoutSummary = document.getElementById('checkoutSummary');
  const subtotalEl      = document.getElementById('subtotalAmount');
  const totalEl         = document.getElementById('totalAmount');
  const reserveBtn      = document.getElementById('reserveBtn');
  const payBtn          = document.getElementById('payBtn');
  const cancelBtn       = document.getElementById('cancelBtn');
  const paymentCard     = document.getElementById('paymentCard');
  const timerEl         = document.getElementById('reservationTimer');
  const timerDisplay    = document.getElementById('timerDisplay');
  const countEl         = document.getElementById('selectionCount');
  const successModal    = document.getElementById('successModal');
  const modalTickets    = document.getElementById('modalTickets');
  const loginBanner     = document.getElementById('loginPromptBanner');

  /* ── Build seat map ─────────────────────────────────────────────── */
  function buildMap() {
    if (!grid) return;
    grid.innerHTML = '';

    if (!SEATS_DATA || SEATS_DATA.length === 0) {
      grid.innerHTML = '<p style="color:var(--c-muted);text-align:center;padding:2rem">No hay asientos disponibles.</p>';
      return;
    }

    const rows = {};
    SEATS_DATA.forEach(s => { (rows[s.row] = rows[s.row] || []).push(s); });

    Object.keys(rows).sort().forEach(rowKey => {
      const seats = rows[rowKey].sort((a, b) => a.number - b.number);
      const rowEl = document.createElement('div');
      rowEl.className = 'seat-row';

      const lbl = document.createElement('span');
      lbl.className = 'row-label';
      lbl.textContent = rowKey;
      rowEl.appendChild(lbl);

      seats.forEach(seat => rowEl.appendChild(makeSeatBtn(seat)));
      grid.appendChild(rowEl);
    });
  }

  function makeSeatBtn(seat) {
    const typeClass   = TYPE_CLASS[seat.type]  ?? 'standard';
    const typeLabel   = TYPE_LABEL[seat.type]  ?? 'Estándar';
    const price       = PRICE_MAP[seat.type]   ?? BASE_PRICE;
    const status      = seatStatus[seat.id] ?? seat.status;
    const statusClass = statusToClass(status);

    const btn = document.createElement('button');
    btn.className   = `seat ${statusClass} ${typeClass}`;
    btn.dataset.id  = seat.id;
    btn.textContent = seat.number;
    btn.title       = `${seat.label} · ${typeLabel} · $${fmt(price)}`;
    btn.setAttribute('aria-label', btn.title);

    if (status === STATUS_AVAILABLE) {
      btn.addEventListener('click', () => toggleSeat(seat, btn));
    } else {
      btn.disabled = true;
    }
    return btn;
  }

  function statusToClass(s) {
    if (s === STATUS_AVAILABLE) return 'available';
    if (s === STATUS_RESERVED)  return 'reserved';
    return 'sold';
  }

  /* ── Real-time polling ──────────────────────────────────────────── */
  let activityTimeout = null;

  function showActivityBanner(msg) {
    let banner = document.getElementById('activityBanner');
    if (!banner) {
      banner = document.createElement('div');
      banner.id = 'activityBanner';
      banner.style.cssText = `
        position:fixed;bottom:1.5rem;left:50%;transform:translateX(-50%);
        background:rgba(251,191,36,.15);border:1px solid rgba(251,191,36,.35);
        color:#fbbf24;padding:.55rem 1.25rem;border-radius:8px;
        font-size:.82rem;font-weight:500;z-index:9000;
        display:flex;align-items:center;gap:.5rem;
        box-shadow:0 4px 20px rgba(0,0,0,.4);white-space:nowrap;
        transition:opacity .4s;`;
      document.body.appendChild(banner);
    }
    banner.innerHTML = `<span>⚡</span> ${msg}`;
    banner.style.opacity = '1';
    clearTimeout(activityTimeout);
    activityTimeout = setTimeout(() => { banner.style.opacity = '0'; }, 4000);
  }

  function startPolling() {
    stopPolling();
    pollInterval = setInterval(pollSeats, POLL_MS);
  }

  function stopPolling() {
    if (pollInterval) { clearInterval(pollInterval); pollInterval = null; }
  }

  async function pollSeats() {
    try {
      const res = await fetch(`/Events/SeatsJson?showtimeId=${SHOWTIME_ID}`, { credentials: 'same-origin' });
      if (!res.ok) return;
      const freshSeats = await res.json();

      let changed = false;
      let newReservations = 0;

      freshSeats.forEach(s => {
        const prev = seatStatus[s.id];
        if (prev !== undefined && prev !== s.status) {
          seatStatus[s.id] = s.status;
          changed = true;

          if (prev === STATUS_AVAILABLE && s.status === STATUS_RESERVED) newReservations++;

          const btn = grid?.querySelector(`[data-id="${s.id}"]`);
          if (btn) {
            const seat = SEATS_DATA.find(x => x.id === s.id);
            const typeClass = TYPE_CLASS[seat?.type ?? 0] ?? 'standard';

            if (s.status !== STATUS_AVAILABLE) {
              if (reservedSeatIds.length === 0) {
                const idx = selectedSeats.findIndex(x => x.id === s.id);
                if (idx > -1) {
                  selectedSeats.splice(idx, 1);
                  toast(`El asiento ${seat?.label ?? s.id} fue tomado por otro usuario.`, 'error');
                }
              }
              if (!reservedSeatIds.includes(s.id)) {
                btn.className = `seat ${statusToClass(s.status)} ${typeClass}`;
                btn.disabled  = true;
                btn.onclick   = null;
              }
            } else {
              if (reservedSeatIds.length === 0) {
                btn.className = `seat available ${typeClass}`;
                btn.disabled  = false;
                btn.style.opacity = '';
                btn.onclick   = null;
                btn.addEventListener('click', () => toggleSeat(seat, btn));
              }
            }
          }
        }
      });

      if (newReservations > 0 && reservedSeatIds.length === 0) {
        const msg = newReservations === 1
          ? 'Alguien está reservando un asiento ahora mismo'
          : `${newReservations} asientos fueron reservados por otro usuario`;
        showActivityBanner(msg);
      }

      if (changed) refreshPanel();
    } catch { /* silent */ }
  }

  /* ── Toggle seat selection ──────────────────────────────────────── */
  function toggleSeat(seat, btn) {
    if (reservedSeatIds.length > 0) return;

    const idx = selectedSeats.findIndex(s => s.id === seat.id);
    if (idx > -1) {
      selectedSeats.splice(idx, 1);
      btn.classList.remove('selected');
    } else {
      if (selectedSeats.length >= MAX_SEATS) {
        toast(`Máximo ${MAX_SEATS} asientos por orden`, 'error');
        return;
      }
      selectedSeats.push(seat);
      btn.classList.add('selected');
    }
    refreshPanel();
  }

  /* ── Sidebar panel ──────────────────────────────────────────────── */
  function refreshPanel() {
    const n = selectedSeats.length;
    if (countEl) {
      countEl.textContent = n === 0
        ? 'Ningún asiento seleccionado'
        : `${n} asiento${n !== 1 ? 's' : ''} seleccionado${n !== 1 ? 's' : ''}`;
    }

    if (n === 0) {
      selectedList.innerHTML = `
        <div class="empty-selection">
          <span>💺</span>
          <p>Haz clic en los asientos disponibles para seleccionarlos</p>
        </div>`;
      if (checkoutSummary) checkoutSummary.style.display = 'none';
      if (reserveBtn) reserveBtn.disabled = true;
      return;
    }

    selectedList.innerHTML = selectedSeats.map(s => {
      const price = PRICE_MAP[s.type] ?? BASE_PRICE;
      const label = TYPE_LABEL[s.type] ?? 'Estándar';
      return `
        <div class="selected-item">
          <div>
            <div class="selected-item-label">💺 ${s.label}</div>
            <div class="selected-item-type">${label}</div>
          </div>
          <div class="selected-item-price">$${fmt(price)}</div>
        </div>`;
    }).join('');

    const total = selectedSeats.reduce((sum, s) => sum + Number(PRICE_MAP[s.type] ?? BASE_PRICE), 0);
    if (subtotalEl) subtotalEl.textContent = `$${fmt(total)}`;
    if (totalEl)    totalEl.textContent    = `$${fmt(total)}`;
    if (checkoutSummary) checkoutSummary.style.display = 'block';

    if (reserveBtn) {
      reserveBtn.disabled = false;
      const lbl = reserveBtn.querySelector('.btn-label');
      if (lbl) lbl.textContent = IS_AUTH ? 'Reservar asientos' : 'Continuar con la compra';
    }
  }

  /* ── STEP 1: Reserve seats (NO order created yet) ───────────────── */
  if (reserveBtn) {
    reserveBtn.addEventListener('click', async () => {
      if (selectedSeats.length === 0) return;

      if (!IS_AUTH) {
        if (loginBanner) loginBanner.style.display = 'block';
        reserveBtn.style.display = 'none';
        return;
      }

      setLoading(reserveBtn, true);

      try {
        const res = await apiFetch('/Orders/Reserve', {
          showtimeId: SHOWTIME_ID,
          seatIds: selectedSeats.map(s => s.id)
        });

        if (!res.ok) {
          const err = await res.json().catch(() => ({}));
          throw new Error(err.message || `Error ${res.status}: No se pudo reservar`);
        }

        const data = await res.json();
        reservedSeatIds = data.reservedSeatIds ?? selectedSeats.map(s => s.id);
        reserveExpiry   = data.expiresAt
          ? new Date(data.expiresAt)
          : new Date(Date.now() + 5 * 60 * 1000);

        // Update UI — NO order created yet
        reserveBtn.style.display = 'none';
        if (payBtn)      payBtn.style.display      = 'flex';
        if (cancelBtn)   cancelBtn.style.display   = 'flex';
        if (paymentCard) paymentCard.style.display = 'block';
        if (timerEl)     timerEl.style.display     = 'flex';

        // Dim non-selected available seats
        grid?.querySelectorAll('.seat.available:not(.selected)').forEach(b => {
          b.disabled = true;
          b.style.opacity = '0.3';
        });

        startTimer();
        toast('¡Asientos reservados! Tienes 5 minutos para pagar.', 'success');

      } catch (err) {
        toast(err.message || 'Error al reservar', 'error');
      } finally {
        setLoading(reserveBtn, false);
      }
    });
  }

  /* ── STEP 2: Pay — creates order first, then pays ───────────────── */
  if (payBtn) {
    payBtn.addEventListener('click', async () => {
      const method = document.querySelector('input[name="paymentMethod"]:checked')?.value || 'CreditCard';
      setLoading(payBtn, true);

      try {
        // Create the order now (seats already reserved)
        const orderRes = await apiFetch('/Orders/Create', {
          showtimeId: SHOWTIME_ID,
          seatIds: reservedSeatIds.length > 0 ? reservedSeatIds : selectedSeats.map(s => s.id)
        });

        if (!orderRes.ok) {
          const err = await orderRes.json().catch(() => ({}));
          throw new Error(err.message || 'No se pudo crear la orden');
        }

        const orderData = await orderRes.json();
        currentOrderId = orderData.id ?? orderData.Id ?? null;

        if (!currentOrderId) throw new Error('No se pudo obtener el ID de la orden');

        // Now pay
        const payRes = await apiFetch('/Orders/Pay', {
          orderId: currentOrderId,
          paymentMethod: method
        });

        if (!payRes.ok) {
          const err = await payRes.json().catch(() => ({}));
          throw new Error(err.message || `Error ${payRes.status}: No se pudo procesar el pago`);
        }

        const data = await payRes.json();
        clearInterval(timerInterval);
        showSuccessModal(data);

      } catch (err) {
        toast(err.message || 'Error en el pago', 'error');
      } finally {
        setLoading(payBtn, false);
      }
    });
  }

  /* ── Cancel — two modes ─────────────────────────────────────────── */
  if (cancelBtn) {
    cancelBtn.addEventListener('click', async () => {
      setLoading(cancelBtn, true);
      try {
        if (hasPendingOrder && PENDING_ORDER?.id) {
          // MODE A: Pending order exists (resumed session)
          // Step 1: release the reservation
          const releaseRes = await apiFetch('/Orders/CancelReservation', { orderId: PENDING_ORDER.id });
          // Step 2: cancel the order
          const cancelRes  = await apiFetch('/Orders/CancelOrder', { orderId: PENDING_ORDER.id });

          clearInterval(timerInterval);
          const releaseOk = releaseRes.ok;
          const cancelOk  = cancelRes.ok;

          if (releaseOk || cancelOk) {
            toast('Orden cancelada. Los asientos han sido liberados.', 'success');
            const banner = document.getElementById('pendingBanner');
            if (banner) banner.style.display = 'none';
            setTimeout(() => location.reload(), 1200);
          } else {
            const errData = await cancelRes.json().catch(() => ({}));
            toast(errData.message || 'No se pudo cancelar la orden', 'error');
          }
        } else {
          // MODE B: Active reservation (no order created yet) — just release seats
          if (reservedSeatIds.length > 0) {
            await apiFetch('/Orders/Release', reservedSeatIds).catch(() => {});
          }
          clearInterval(timerInterval);
          resetFlow();
          toast('Reserva cancelada. Puedes elegir otros asientos.', 'success');
        }
      } catch {
        toast('Error de conexión. Intenta de nuevo.', 'error');
      } finally {
        setLoading(cancelBtn, false);
      }
    });
  }

  /* ── Timer ──────────────────────────────────────────────────────── */
  function startTimer() {
    clearInterval(timerInterval);
    timerInterval = setInterval(() => {
      const remaining = Math.max(0, reserveExpiry - Date.now());
      const m = Math.floor(remaining / 60000);
      const s = Math.floor((remaining % 60000) / 1000);
      if (timerDisplay) {
        timerDisplay.textContent = `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
        timerDisplay.style.color = remaining < 60000 ? 'var(--c-red)' : '';
      }
      if (remaining <= 0) {
        clearInterval(timerInterval);
        toast('La reserva expiró. Por favor selecciona nuevamente.', 'error');
        resetFlow();
      }
    }, 1000);
  }

  /* ── Full reset ─────────────────────────────────────────────────── */
  function resetFlow() {
    selectedSeats   = [];
    reservedSeatIds = [];
    currentOrderId  = null;

    grid?.querySelectorAll('.seat').forEach(btn => {
      btn.classList.remove('selected');
      btn.style.opacity = '';
      const seatId = parseInt(btn.dataset.id);
      const s      = seatStatus[seatId] ?? STATUS_AVAILABLE;
      if (s === STATUS_AVAILABLE) {
        btn.disabled = false;
        const seat = SEATS_DATA.find(x => x.id === seatId);
        if (seat) {
          const newBtn = btn.cloneNode(true);
          newBtn.addEventListener('click', () => toggleSeat(seat, newBtn));
          btn.parentNode?.replaceChild(newBtn, btn);
        }
      } else {
        btn.disabled = true;
      }
    });

    if (reserveBtn)  { reserveBtn.style.display = 'flex'; reserveBtn.disabled = true; }
    if (payBtn)      payBtn.style.display      = 'none';
    if (cancelBtn)   cancelBtn.style.display   = 'none';
    if (paymentCard) paymentCard.style.display = 'none';
    if (timerEl)     timerEl.style.display     = 'none';
    if (loginBanner) loginBanner.style.display = 'none';
    if (timerDisplay) timerDisplay.style.color = '';

    refreshPanel();
    startPolling();
  }

  /* ── Success modal ──────────────────────────────────────────────── */
  function showSuccessModal(data) {
    if (modalTickets) {
      const tickets = data.tickets ?? data.Tickets ?? [];
      modalTickets.innerHTML = tickets.length
        ? tickets.map(t => `<span class="modal-ticket-badge">💺 ${t.seatLabel ?? t.SeatLabel}</span>`).join('')
        : '';
    }
    if (successModal) successModal.style.display = 'flex';
    stopPolling();
  }

  /* ── Helpers ────────────────────────────────────────────────────── */
  function apiFetch(url, body) {
    return fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'RequestVerificationToken': antiForgery()
      },
      body: JSON.stringify(body)
    });
  }

  function antiForgery() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value
      || document.querySelector('meta[name="csrf-token"]')?.content
      || '';
  }

  function setLoading(btn, on) {
    if (!btn) return;
    btn.disabled = on;
    btn.querySelector('.btn-label')?.classList.toggle('hidden', on);
    btn.querySelector('.btn-spinner')?.classList.toggle('hidden', !on);
  }

  function fmt(n) {
    return Number(n).toLocaleString('es-CO', { minimumFractionDigits: 0 });
  }

  function toast(msg, type = 'success') {
    document.querySelector('.toast-dynamic')?.remove();
    const t = document.createElement('div');
    t.className = `toast toast--${type === 'error' ? 'error' : 'success'} toast-dynamic`;
    t.innerHTML = `<span class="toast-icon">${type === 'error' ? '✕' : '✓'}</span> ${msg}`;
    document.body.appendChild(t);
    setTimeout(() => {
      t.style.transition = 'opacity .5s';
      t.style.opacity = '0';
      setTimeout(() => t.remove(), 500);
    }, 4000);
  }

  /* ── Init ───────────────────────────────────────────────────────── */
  buildMap();

  if (hasPendingOrder) {
    currentOrderId  = PENDING_ORDER.id;
    reservedSeatIds = [];
    grid?.querySelectorAll('.seat.available').forEach(btn => {
      btn.disabled = true;
      btn.style.opacity = '0.35';
      btn.title = 'Tienes una orden pendiente de pago';
    });
    document.getElementById('resumePayBtn')?.addEventListener('click', () => {
      document.getElementById('payBtn')?.click();
    });
    document.getElementById('resumeCancelBtn')?.addEventListener('click', () => {
      document.getElementById('cancelBtn')?.click();
    });
    const banner = document.getElementById('pendingBanner');
    if (banner) {
      let pulses = 0;
      const pulse = setInterval(() => {
        banner.style.boxShadow = pulses % 2 === 0
          ? '0 0 0 3px rgba(251,191,36,.45)'
          : '0 0 0 0 transparent';
        if (++pulses >= 6) clearInterval(pulse);
      }, 500);
    }
  }

  startPolling();
})();
