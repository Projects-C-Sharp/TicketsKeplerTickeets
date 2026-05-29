/**
 * seats.js — Seat selection, reservation, order & payment flow
 * Depends on: SEATS_DATA, SHOWTIME_ID, BASE_PRICE, IS_AUTH, PRICE_MAP, TYPE_LABEL, TYPE_CLASS
 */
(function () {
  'use strict';

  // ─── State ──────────────────────────────────────────────────────
  let selectedSeats = [];  // SeatDto objects
  let reservedSeatIds = [];
  let currentOrderId = null;
  let timerInterval = null;
  let reserveExpiry = null;
  const MAX_SEATS = 8;

  // ─── DOM refs ────────────────────────────────────────────────────
  const grid          = document.getElementById('seatGrid');
  const selectedList  = document.getElementById('selectedList');
  const checkoutSummary = document.getElementById('checkoutSummary');
  const subtotalEl    = document.getElementById('subtotalAmount');
  const totalEl       = document.getElementById('totalAmount');
  const reserveBtn    = document.getElementById('reserveBtn');
  const payBtn        = document.getElementById('payBtn');
  const cancelBtn     = document.getElementById('cancelBtn');
  const paymentCard   = document.getElementById('paymentCard');
  const timerEl       = document.getElementById('reservationTimer');
  const timerDisplay  = document.getElementById('timerDisplay');
  const countEl       = document.getElementById('selectionCount');
  const successModal  = document.getElementById('successModal');
  const modalTickets  = document.getElementById('modalTickets');

  // ─── Build seat map ──────────────────────────────────────────────
  function buildMap() {
    if (!grid || !SEATS_DATA.length) return;

    // Group by row
    const rows = {};
    SEATS_DATA.forEach(s => { (rows[s.row] = rows[s.row] || []).push(s); });

    Object.keys(rows).sort().forEach(rowKey => {
      const seats = rows[rowKey].sort((a, b) => a.number - b.number);
      const rowEl = document.createElement('div');
      rowEl.className = 'seat-row';

      // Row label
      const label = document.createElement('span');
      label.className = 'row-label';
      label.textContent = rowKey;
      rowEl.appendChild(label);

      seats.forEach(seat => {
        const btn = document.createElement('button');
        btn.className = `seat ${getStatusClass(seat)} ${TYPE_CLASS[seat.type] || 'standard'}`;
        btn.dataset.id = seat.id;
        btn.title = `${seat.label} — ${TYPE_LABEL[seat.type]} — $${formatPrice(PRICE_MAP[seat.type])}`;
        btn.setAttribute('aria-label', btn.title);

        btn.textContent = seat.number;

        if (seat.status === 0) { // Available
          btn.addEventListener('click', () => toggleSeat(seat, btn));
        } else {
          btn.disabled = true;
        }

        rowEl.appendChild(btn);
      });

      grid.appendChild(rowEl);
    });
  }

  function getStatusClass(seat) {
    if (seat.status === 0) return 'available';
    if (seat.status === 1) return 'reserved';
    return 'sold';
  }

  function formatPrice(n) {
    return Number(n).toLocaleString('es-CO', { minimumFractionDigits: 0 });
  }

  // ─── Seat toggle ─────────────────────────────────────────────────
  function toggleSeat(seat, btn) {
    if (!IS_AUTH) return;
    if (reservedSeatIds.length > 0) return; // Already reserved

    const idx = selectedSeats.findIndex(s => s.id === seat.id);
    if (idx > -1) {
      selectedSeats.splice(idx, 1);
      btn.classList.remove('selected');
    } else {
      if (selectedSeats.length >= MAX_SEATS) {
        showToast(`Máximo ${MAX_SEATS} asientos por orden`, 'error');
        return;
      }
      selectedSeats.push(seat);
      btn.classList.add('selected');
    }
    updatePanel();
  }

  // ─── Update sidebar ──────────────────────────────────────────────
  function updatePanel() {
    const n = selectedSeats.length;
    countEl.textContent = n === 0 ? 'Ningún asiento seleccionado' : `${n} asiento${n > 1 ? 's' : ''} seleccionado${n > 1 ? 's' : ''}`;

    if (n === 0) {
      selectedList.innerHTML = `
        <div class="empty-selection">
          <span>💺</span>
          <p>Haz clic en los asientos disponibles para seleccionarlos</p>
        </div>`;
      checkoutSummary.style.display = 'none';
      if (reserveBtn) reserveBtn.disabled = true;
      return;
    }

    // Render selected items
    selectedList.innerHTML = selectedSeats.map(s => `
      <div class="selected-item">
        <div>
          <div class="selected-item-label">💺 ${s.label}</div>
          <div class="selected-item-type">${TYPE_LABEL[s.type]}</div>
        </div>
        <div class="selected-item-price">$${formatPrice(PRICE_MAP[s.type])}</div>
      </div>
    `).join('');

    const total = selectedSeats.reduce((sum, s) => sum + Number(PRICE_MAP[s.type]), 0);
    subtotalEl.textContent = `$${formatPrice(total)}`;
    totalEl.textContent    = `$${formatPrice(total)}`;
    checkoutSummary.style.display = 'block';

    if (reserveBtn) reserveBtn.disabled = false;
  }

  // ─── Reserve ─────────────────────────────────────────────────────
  if (reserveBtn) {
    reserveBtn.addEventListener('click', async () => {
      setLoading(reserveBtn, true);
      try {
        const res = await fetch('/Orders/Reserve', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgery() },
          body: JSON.stringify({ showtimeId: SHOWTIME_ID, seatIds: selectedSeats.map(s => s.id) })
        });
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || 'No se pudo reservar');

        reservedSeatIds = data.seatIds || selectedSeats.map(s => s.id);
        reserveExpiry   = new Date(data.expiresAt);

        // Disable reserve btn, show pay/cancel
        reserveBtn.style.display = 'none';
        payBtn.style.display     = 'flex';
        cancelBtn.style.display  = 'flex';
        paymentCard.style.display = 'block';
        timerEl.style.display    = 'flex';

        // Disable all unselected seats
        document.querySelectorAll('.seat.available:not(.selected)').forEach(btn => {
          btn.disabled = true;
          btn.style.opacity = '.3';
        });

        startTimer();
        showToast('¡Asientos reservados por 5 minutos!', 'success');

        // Create order
        const orderRes = await fetch('/Orders/Create', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgery() },
          body: JSON.stringify({ seatIds: reservedSeatIds })
        });
        const orderData = await orderRes.json();
        if (orderRes.ok) currentOrderId = orderData.id;
      } catch (err) {
        showToast(err.message, 'error');
      } finally {
        setLoading(reserveBtn, false);
      }
    });
  }

  // ─── Pay ─────────────────────────────────────────────────────────
  if (payBtn) {
    payBtn.addEventListener('click', async () => {
      if (!currentOrderId) {
        showToast('Error: No se encontró la orden.', 'error');
        return;
      }
      const method = document.querySelector('input[name="paymentMethod"]:checked')?.value || 'CreditCard';

      setLoading(payBtn, true);
      try {
        const res = await fetch('/Orders/Pay', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgery() },
          body: JSON.stringify({ orderId: currentOrderId, paymentMethod: method })
        });
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || 'Error en el pago');

        clearInterval(timerInterval);
        showSuccessModal(data);
      } catch (err) {
        showToast(err.message, 'error');
      } finally {
        setLoading(payBtn, false);
      }
    });
  }

  // ─── Cancel ──────────────────────────────────────────────────────
  if (cancelBtn) {
    cancelBtn.addEventListener('click', async () => {
      await fetch('/Orders/Release', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgery() },
        body: JSON.stringify(reservedSeatIds)
      });
      clearInterval(timerInterval);
      resetToSelection();
      showToast('Reserva cancelada', 'info');
    });
  }

  // ─── Timer ───────────────────────────────────────────────────────
  function startTimer() {
    timerInterval = setInterval(() => {
      const remaining = Math.max(0, reserveExpiry - Date.now());
      const m = Math.floor(remaining / 60000);
      const s = Math.floor((remaining % 60000) / 1000);
      if (timerDisplay) timerDisplay.textContent = `${String(m).padStart(2,'0')}:${String(s).padStart(2,'0')}`;
      if (remaining <= 0) {
        clearInterval(timerInterval);
        showToast('La reserva expiró. Por favor selecciona nuevamente.', 'error');
        resetToSelection();
      }
    }, 1000);
  }

  function resetToSelection() {
    selectedSeats = [];
    reservedSeatIds = [];
    currentOrderId = null;

    document.querySelectorAll('.seat').forEach(btn => {
      btn.classList.remove('selected');
      btn.style.opacity = '';
      const seatId = parseInt(btn.dataset.id);
      const seat = SEATS_DATA.find(s => s.id === seatId);
      if (seat && seat.status === 0) btn.disabled = false;
    });

    if (reserveBtn) { reserveBtn.style.display = 'flex'; reserveBtn.disabled = true; }
    if (payBtn)     payBtn.style.display = 'none';
    if (cancelBtn)  cancelBtn.style.display = 'none';
    if (paymentCard) paymentCard.style.display = 'none';
    if (timerEl)    timerEl.style.display = 'none';

    updatePanel();
  }

  // ─── Success modal ───────────────────────────────────────────────
  function showSuccessModal(data) {
    if (modalTickets && data.tickets?.length) {
      modalTickets.innerHTML = data.tickets.map(t =>
        `<span class="modal-ticket-badge">💺 ${t.seatLabel}</span>`
      ).join('');
    }
    if (successModal) successModal.style.display = 'flex';
  }

  // ─── Helpers ─────────────────────────────────────────────────────
  function getAntiForgery() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value
        || document.querySelector('meta[name="csrf-token"]')?.content
        || '';
  }

  function setLoading(btn, loading) {
    if (!btn) return;
    const label   = btn.querySelector('.btn-label') || btn;
    const spinner = btn.querySelector('.btn-spinner');
    btn.disabled = loading;
    if (spinner) { spinner.classList.toggle('hidden', !loading); }
    if (label !== btn) { label.classList.toggle('hidden', loading); }
  }

  function showToast(msg, type = 'success') {
    const existing = document.querySelector('.toast-dynamic');
    if (existing) existing.remove();
    const t = document.createElement('div');
    t.className = `toast toast--${type === 'error' ? 'error' : 'success'} toast-dynamic`;
    t.innerHTML = `<span class="toast-icon">${type === 'error' ? '✕' : '✓'}</span> ${msg}`;
    document.body.appendChild(t);
    setTimeout(() => { t.style.opacity = '0'; t.style.transition = 'opacity .5s'; setTimeout(() => t.remove(), 500); }, 4000);
  }

  // ─── Init ────────────────────────────────────────────────────────
  buildMap();

})();
