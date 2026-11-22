/*
  FCFinance - Tela WhatsApp: conectar o telefone ao bot e simulador de conversa.
  Usa a API em api/WhatsApp (o token de login é enviado pelo apiAuth.js).
*/
const API_BASE_URL = "https://localhost:7148/api";
const WA = `${API_BASE_URL}/WhatsApp`;
const $ = (id) => document.getElementById(id);
const esc = (v) => String(v ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const fmtDateTime = (v) => v ? new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" }).format(new Date(v)) : "";

async function api(url, options = {}) {
  const res = await fetch(url, { headers: { "Accept": "application/json", "Content-Type": "application/json" }, ...options });
  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(text && !text.trim().startsWith("{") && text.length < 200 ? text : `Erro ${res.status}`);
  }
  return res.status === 204 ? null : res.json();
}

let toastTimer;
function toast(msg, error = false) {
  const t = $("toast"); t.textContent = msg; t.className = `toast${error ? " error" : ""}`;
  clearTimeout(toastTimer); toastTimer = setTimeout(() => t.classList.add("hidden"), 3500);
}
function showError(msg) { const el = $("wa-status-msg"); el.textContent = msg; el.classList.remove("hidden"); }

/* ---------- Conexão do telefone ---------- */
let countdown, poll;
async function loadStatus() {
  try {
    const s = await api(`${WA}/status`);
    $("wa-loading").classList.add("hidden");
    $("wa-connected").classList.toggle("hidden", !s.connected);
    $("wa-disconnected").classList.toggle("hidden", s.connected);
    $("wa-not-configured").classList.toggle("hidden", s.botConfigured);
    if (s.connected) {
      $("wa-phone").textContent = s.phone;
      $("wa-since").textContent = s.linkedAt ? `Conectado em ${fmtDateTime(s.linkedAt)}` : "";
      stopCountdown();
    }
  } catch (e) {
    showError(`${e.message}. Confirme se a API está rodando.`);
  }
}

function stopCountdown() { clearInterval(countdown); clearInterval(poll); $("wa-code-box").classList.add("hidden"); }

$("wa-generate").addEventListener("click", async () => {
  const btn = $("wa-generate"); btn.disabled = true;
  try {
    const c = await api(`${WA}/link-code`, { method: "POST" });
    $("wa-code").textContent = c.code;
    $("wa-code-box").classList.remove("hidden");
    btn.textContent = "↻ Gerar outro código";
    const expires = new Date(c.expiresAt).getTime();
    clearInterval(countdown);
    const tick = () => {
      const left = Math.max(0, Math.round((expires - Date.now()) / 1000));
      $("wa-code-expires").textContent = left > 0
        ? `vence em ${Math.floor(left / 60)}:${String(left % 60).padStart(2, "0")}`
        : "código vencido: gere outro";
      if (left === 0) clearInterval(countdown);
    };
    tick(); countdown = setInterval(tick, 1000);
    // Confere de tempos em tempos se o usuário já enviou o código pelo WhatsApp.
    clearInterval(poll);
    poll = setInterval(async () => {
      if (Date.now() > expires) return clearInterval(poll);
      const s = await api(`${WA}/status`).catch(() => null);
      if (s?.connected) { clearInterval(poll); toast("WhatsApp conectado! ✅"); loadStatus(); }
    }, 4000);
  } catch (e) {
    toast(e.message || "Não foi possível gerar o código.", true);
  } finally { btn.disabled = false; }
});

$("wa-unlink").addEventListener("click", async () => {
  if (!confirm("Desconectar este número? O bot não vai mais registrar lançamentos dele na sua conta.")) return;
  try {
    await api(`${WA}/link`, { method: "DELETE" });
    toast("Número desconectado.");
    loadStatus();
  } catch (e) { toast(e.message || "Não foi possível desconectar.", true); }
});

/* ---------- Simulador ---------- */
// O bot usa *negrito* no estilo do WhatsApp: converte para <b> (depois de escapar o HTML).
const formatBot = (text) => esc(text).replace(/\*([^*\n]+)\*/g, "<b>$1</b>");

function addBubble(html, who) {
  const div = document.createElement("div");
  div.className = `wa-bubble ${who}`;
  div.innerHTML = html;
  $("wa-chat").appendChild(div);
  $("wa-chat").scrollTop = $("wa-chat").scrollHeight;
  return div;
}

$("wa-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const text = $("wa-input").value.trim();
  if (!text) return;
  $("wa-input").value = "";
  addBubble(esc(text), "me");
  const typing = addBubble("digitando...", "bot typing");
  $("wa-send").disabled = true;
  try {
    const r = await api(`${WA}/simulate`, { method: "POST", body: JSON.stringify({ text }) });
    typing.remove();
    addBubble(formatBot(r.reply), "bot");
  } catch (err) {
    typing.remove();
    addBubble(`⚠️ ${esc(err.message)}. Confirme se a API está rodando.`, "bot");
  } finally {
    $("wa-send").disabled = false;
    $("wa-input").focus();
  }
});

loadStatus();
