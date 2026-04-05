/*
 * [TR] Bu dosya ne işe yarar: Uygulama genelinde küçük istemci davranışları.
 * [TR] Neden gerekli: Form submit loading-state ve kullanıcı geri bildirimi tutarlılığı sağlar.
 * [TR] İlgili: _Layout.cshtml, tüm POST formları
 *
 * MODIFICATION NOTES (TR)
 * - Gelecekte toast queue ve global error handler eklenebilir.
 * - Form bazlı özel loading metinleri data-loading-text ile artırılabilir.
 * - Genel resim OCR desteği bu sürümde yer almamaktadır.
 */
(function () {
  const loadingForms = document.querySelectorAll("form[data-loading-form='true']");
  for (const form of loadingForms) {
    form.addEventListener("submit", function () {
      const submit = form.querySelector("button[type='submit']");
      if (!submit) return;
      if (submit.dataset.loadingApplied === "true") return;

      submit.dataset.originalText = submit.innerHTML;
      submit.dataset.loadingApplied = "true";
      submit.disabled = true;
      submit.innerHTML =
        "<span class='spinner-border spinner-border-sm me-2' role='status' aria-hidden='true'></span>İşleniyor...";
    });
  }
})();
