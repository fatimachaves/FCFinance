// Menu lateral (usado pelo _Layout em todas as páginas logadas).
//  - Computador: recolhe/expande e lembra a escolha.
//  - Celular: vira uma gaveta aberta pelo botão ☰; fecha ao escolher um item, tocar fora ou apertar Esc.
//  - Marca como ativo o item da aba atual de Transações (#nova, #historico, #metas, #categorias).
(function () {
  const sidebar = document.getElementById("sidebar");
  const desktopToggle = document.getElementById("sidebar-toggle");
  const mobileToggle = document.getElementById("mobile-menu");
  if (!sidebar) return;
  const isMobile = () => window.matchMedia("(max-width: 850px)").matches;

  // ----- Computador: recolher/expandir -----
  function setSidebarCollapsed(collapsed) {
    sidebar.classList.toggle("collapsed", collapsed);
    desktopToggle.setAttribute("aria-expanded", String(!collapsed));
    desktopToggle.setAttribute("aria-label", collapsed ? "Expandir menu" : "Recolher menu");
    desktopToggle.title = collapsed ? "Expandir menu" : "Recolher menu";
    desktopToggle.textContent = collapsed ? "›" : "‹";
    try { localStorage.setItem("fcfinance-sidebar-collapsed", String(collapsed)); } catch (_) {}
  }
  let collapsed = false;
  try { collapsed = localStorage.getItem("fcfinance-sidebar-collapsed") === "true"; } catch (_) {}
  setSidebarCollapsed(collapsed);
  desktopToggle.addEventListener("click", () => setSidebarCollapsed(!sidebar.classList.contains("collapsed")));

  // ----- Celular: gaveta -----
  function setOpen(open) {
    sidebar.classList.toggle("open", open);
    if (!mobileToggle) return;
    mobileToggle.setAttribute("aria-expanded", String(open));
    mobileToggle.setAttribute("aria-label", open ? "Fechar menu" : "Abrir menu");
  }
  mobileToggle?.addEventListener("click", (e) => { e.stopPropagation(); setOpen(!sidebar.classList.contains("open")); });
  document.addEventListener("click", (e) => {
    if (isMobile() && sidebar.classList.contains("open") && !sidebar.contains(e.target)) setOpen(false);
  });
  document.addEventListener("keydown", (e) => { if (e.key === "Escape") setOpen(false); });
  sidebar.querySelectorAll(".nav-link").forEach(link => link.addEventListener("click", () => { if (isMobile()) setOpen(false); }));

  // ----- Item ativo nas abas de Transações -----
  function markActiveTab() {
    const links = sidebar.querySelectorAll(".nav-link[data-hash]");
    if (!links.length) return;
    const samePage = (a) => new URL(a.href, location.href).pathname.toLowerCase() === location.pathname.toLowerCase();
    const hash = location.hash.slice(1) || "nova";
    links.forEach(a => a.classList.toggle("active", samePage(a) && a.dataset.hash === hash));
  }
  markActiveTab();
  window.addEventListener("hashchange", markActiveTab);
  window.addEventListener("fcf:tab", markActiveTab); // disparado pela tela de Transações ao trocar de aba
})();
