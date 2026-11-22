// Menu lateral (usado pelo _Layout em todas as páginas)
(function () {
  const sidebar = document.getElementById("sidebar");
  const desktopToggle = document.getElementById("sidebar-toggle");
  const mobileToggle = document.getElementById("mobile-menu");
  if (!sidebar) return;

  // Desktop: recolhe/expande e lembra a escolha do usuário.
  function setSidebarCollapsed(collapsed) {
    sidebar.classList.toggle("collapsed", collapsed);
    desktopToggle.setAttribute("aria-expanded", String(!collapsed));
    desktopToggle.setAttribute("aria-label", collapsed ? "Expandir menu" : "Recolher menu");
    desktopToggle.title = collapsed ? "Expandir menu" : "Recolher menu";
    desktopToggle.textContent = collapsed ? "›" : "‹";
    try { localStorage.setItem("fcfinance-sidebar-collapsed", String(collapsed)); } catch (_) {}
  }

  try {
    setSidebarCollapsed(localStorage.getItem("fcfinance-sidebar-collapsed") === "true");
  } catch (_) {
    setSidebarCollapsed(false);
  }

  desktopToggle.addEventListener("click", () =>
    setSidebarCollapsed(!sidebar.classList.contains("collapsed")));

  // Mobile: abre como painel sobre o conteúdo e fecha ao escolher um item.
  mobileToggle.addEventListener("click", () => {
    const isOpen = sidebar.classList.toggle("open");
    mobileToggle.setAttribute("aria-expanded", String(isOpen));
    mobileToggle.setAttribute("aria-label", isOpen ? "Fechar menu" : "Abrir menu");
  });

  sidebar.querySelectorAll(".nav-link").forEach(link => {
    link.addEventListener("click", () => {
      if (window.matchMedia("(max-width: 850px)").matches) {
        sidebar.classList.remove("open");
        mobileToggle.setAttribute("aria-expanded", "false");
      }
    });
  });
})();
