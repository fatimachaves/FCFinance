/*
  FCFinance - Minha conta: botão "Baixar meus dados" (LGPD).
  Busca na API todos os dados do usuário e baixa um arquivo .json.
*/
(function () {
  const API = document.querySelector('meta[name="fcf-api-url"]')?.content || "https://localhost:7148/api";
  const btn = document.getElementById("exportar-dados");
  const status = document.getElementById("exportar-status");
  if (!btn) return;

  btn.addEventListener("click", async () => {
    btn.disabled = true;
    status.textContent = "Preparando seus dados...";
    try {
      const res = await fetch(`${API}/MinhaConta/exportar`, { headers: { "Accept": "application/json" } });
      if (!res.ok) throw new Error(`Erro ${res.status}`);
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `fcfinance-meus-dados-${new Date().toISOString().slice(0, 10)}.json`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
      status.textContent = "Pronto! O arquivo foi baixado.";
    } catch (e) {
      status.textContent = `Não foi possível baixar os dados (${e.message}). Confirme se a API está rodando.`;
    } finally {
      btn.disabled = false;
    }
  });
})();
