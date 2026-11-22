// Formulários que pedem confirmação antes de enviar (ex.: excluir usuário): <form data-confirm="Texto">.
// Fica aqui (e não em onsubmit="...") porque a política de segurança (CSP) bloqueia JavaScript dentro do HTML.
document.querySelectorAll("form[data-confirm]").forEach(form => {
  form.addEventListener("submit", (e) => {
    if (!confirm(form.dataset.confirm)) e.preventDefault();
  });
});

// Botão de mostrar/ocultar senha (telas de login, alterar senha e usuários).
document.querySelectorAll("[data-toggle-pass]").forEach(btn => {
  btn.addEventListener("click", () => {
    const input = document.getElementById(btn.dataset.togglePass);
    const show = input.type === "password";
    input.type = show ? "text" : "password";
    btn.setAttribute("aria-label", show ? "Ocultar senha" : "Mostrar senha");
    btn.title = btn.getAttribute("aria-label");
    btn.classList.toggle("on", show);
  });
});
