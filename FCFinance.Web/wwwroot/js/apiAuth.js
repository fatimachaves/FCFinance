/*
  Envia o token de login em todas as chamadas à API (fetch) feitas pelas páginas.
  O token é gerado pelo site e fica na <meta name="fcf-api-token"> do _Layout.
  Carregado antes dos outros scripts, então funciona para dashboard.js, transacoes.js etc.
*/
(function () {
  const token = document.querySelector('meta[name="fcf-api-token"]')?.content;
  if (!token) return;
  // Endereço da API: vem da <meta name="fcf-api-url"> (appsettings "Api:BaseUrl"); localhost como reserva.
  const apiUrl = document.querySelector('meta[name="fcf-api-url"]')?.content;
  const API_HOSTS = ["localhost:7148", "localhost:5286"];
  try { if (apiUrl) API_HOSTS.push(new URL(apiUrl, location.href).host); } catch { }
  const isApi = (url) => { try { return API_HOSTS.includes(new URL(url, location.href).host); } catch { return false; } };

  const originalFetch = window.fetch.bind(window);
  window.fetch = async (input, init = {}) => {
    const url = typeof input === "string" ? input : input.url;
    if (!isApi(url)) return originalFetch(input, init);

    const headers = new Headers(init.headers || (typeof input === "string" ? undefined : input.headers));
    headers.set("Authorization", `Bearer ${token}`);
    const response = await originalFetch(input, { ...init, headers });

    // 401 = token vencido. Recarrega a página uma vez para o site gerar um token novo
    // (se o login do site também tiver vencido, a página vai para a tela de login).
    if (response.status === 401) {
      let last = 0; try { last = Number(sessionStorage.getItem("fcf-reload-401") || 0); } catch { }
      if (Date.now() - last > 60000) {
        try { sessionStorage.setItem("fcf-reload-401", String(Date.now())); } catch { }
        location.reload();
      }
    }
    return response;
  };
})();
