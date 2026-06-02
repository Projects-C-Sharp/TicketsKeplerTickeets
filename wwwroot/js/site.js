// ─── Nav toggle ───────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  const toggle = document.getElementById('navToggle');
  const mobile = document.getElementById('navMobile');
  if (toggle && mobile) {
    toggle.addEventListener('click', () => mobile.classList.toggle('is-open'));
  }

  // Auto-dismiss toasts
  const toast = document.getElementById('globalToast');
  if (toast) {
    setTimeout(() => { toast.style.opacity = '0'; toast.style.transition = 'opacity .5s'; setTimeout(() => toast.remove(), 500); }, 4000);
  }

  // Payment method selection
  document.querySelectorAll('.payment-option').forEach(opt => {
    opt.addEventListener('click', () => {
      document.querySelectorAll('.payment-option').forEach(o => o.classList.remove('payment-option--active'));
      opt.classList.add('payment-option--active');
    });
  });
});

// ─── Favorites ────────────────────────────────────────────────────

/** Read favorite IDs stored in the cookie (set by server) */
function getFavIds() {
  try {
    var raw = document.cookie.split(';').map(c => c.trim()).find(c => c.startsWith('tx_favorites='));
    if (!raw) return [];
    return JSON.parse(decodeURIComponent(raw.split('=').slice(1).join('='))) || [];
  } catch(e) { return []; }
}

/** Mark all .fav-btn[data-event-id] buttons according to current favorites */
function initFavButtons() {
  var ids = getFavIds();
  document.querySelectorAll('.fav-btn[data-event-id]').forEach(function(btn) {
    var id = parseInt(btn.getAttribute('data-event-id'));
    if (ids.indexOf(id) !== -1) {
      btn.classList.add('is-fav');
      btn.textContent = '♥';
    } else {
      btn.classList.remove('is-fav');
      btn.textContent = '♡';
    }
  });
}

/** Toggle a favorite by event ID — called from card onclick */
function toggleFav(btn, eventId) {
  var wasFav = btn.classList.contains('is-fav');

  // Optimistic UI update
  btn.classList.toggle('is-fav');
  btn.textContent = btn.classList.contains('is-fav') ? '♥' : '♡';

  fetch('/Favorites/Toggle', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ eventId: eventId })
  })
  .then(function(r) { return r.ok ? r.json() : null; })
  .then(function(data) {
    if (!data) { revert(); return; }
    // Show micro-toast
    showFavToast(data.isFav ? '♥ Guardado en favoritos' : '♡ Eliminado de favoritos', data.isFav);
  })
  .catch(function() { revert(); });

  function revert() {
    btn.classList.toggle('is-fav', wasFav);
    btn.textContent = wasFav ? '♥' : '♡';
  }
}

/** Small non-intrusive toast for favorites */
function showFavToast(msg, isFav) {
  var existing = document.getElementById('favToast');
  if (existing) existing.remove();
  var t = document.createElement('div');
  t.id = 'favToast';
  t.style.cssText = 'position:fixed;bottom:1.5rem;left:50%;transform:translateX(-50%);z-index:9999;background:'
    + (isFav ? 'rgba(255,0,110,.15)' : 'rgba(255,255,255,.08)')
    + ';border:1px solid ' + (isFav ? 'rgba(255,0,110,.4)' : 'rgba(255,255,255,.15)')
    + ';color:#f0f0ff;padding:.6rem 1.25rem;border-radius:99px;font-size:.82rem;font-weight:600;'
    + 'backdrop-filter:blur(12px);animation:slideUp .25s ease;pointer-events:none;';
  t.textContent = msg;
  document.body.appendChild(t);
  setTimeout(function() { t.style.opacity='0'; t.style.transition='opacity .4s'; setTimeout(function(){ t.remove(); },400); }, 2200);
}
