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
