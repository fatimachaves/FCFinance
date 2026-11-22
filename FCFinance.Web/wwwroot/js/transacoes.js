/*
  FCFinance - Tela de Transações (abas: Nova Transação, Metas, Categorias, Histórico).
  Ajuste API_BASE_URL, ENDPOINTS e TYPE_IDS para corresponder à sua API.
*/
// Endereço da API vem da página (<meta name="fcf-api-url">, configurado em appsettings "Api:BaseUrl").
const API_BASE_URL = document.querySelector('meta[name="fcf-api-url"]')?.content || "https://localhost:7148/api";
const ENDPOINTS = {
  values: `${API_BASE_URL}/RegisteredValue`, // GET lista | POST cria | PUT/DELETE {id}
  categories: `${API_BASE_URL}/Category`,    // GET lista | POST cria | PUT/DELETE {id}
  goals: `${API_BASE_URL}/Goals`             // GET lista | POST cria | PUT/DELETE {id}
};
// Confira os IDs na tabela ValueTypes do seu banco (1 = Receita, 2 = Despesa).
const TYPE_IDS = { income: 1, expense: 2 };

const CURRENCY = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const PALETTE = ["#269cf0", "#19b86f", "#e0443e", "#9650dc", "#ffa51e", "#19c7bb", "#ed638a"];
const $ = (id) => document.getElementById(id);
const money = (v) => CURRENCY.format(Number(v) || 0);
const pick = (o, ...keys) => { for (const k of keys) if (o && o[k] != null) return o[k]; };
const esc = (v) => String(v ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const fmtDate = (d) => d ? new Intl.DateTimeFormat("pt-BR").format(d) : "—";
const toInputDate = (d) => d ? d.toLocaleDateString("sv-SE") : "";      // yyyy-mm-dd (data local)
const parseDate = (v) => { const d = new Date(v || ""); return isNaN(d) ? null : d; };
const byDateDesc = (a, b) => (b.date?.getTime() || 0) - (a.date?.getTime() || 0);

let categories = [], goals = [], values = [];
let txType = new URLSearchParams(location.search).get("tipo") === "despesa" ? "expense" : "income";
let goalScope = "month";      // pílula do resumo de metas (aba Nova Transação)
let goalFilter = "all";       // pílula da lista de metas (aba Metas)
let catFilter = "all";        // pílula da lista de categorias (aba Categorias)
let goalType = "month";       // tipo escolhido no formulário de meta
let catType = "income";       // tipo escolhido no formulário de categoria
let editingTxId = null, editingGoal = null, editingCatId = null;

/* ---------- API ---------- */
async function api(url, options) {
  const res = await fetch(url, { headers: { "Accept": "application/json", "Content-Type": "application/json" }, ...options });
  if (!res.ok) {
    // Mensagens curtas em texto (ex.: "Categoria em uso...") vindas da API são mostradas para o usuário.
    const text = await res.text().catch(() => "");
    const friendly = text && !text.trim().startsWith("{") && text.length < 200 ? text : "";
    const err = new Error(friendly || `Erro ${res.status} em ${url}`);
    err.friendly = friendly;
    throw err;
  }
  return res.status === 204 ? null : res.json().catch(() => null);
}
const list = (r) => Array.isArray(r) ? r : (r?.data || r?.items || []);

/* ---------- Normalização (deixa os dados da API num formato único) ---------- */
const normCategory = (c) => {
  const typeId = Number(pick(c, "typeId", "TypeId", "categoryTypeId", "CategoryTypeId"));
  return {
    id: Number(pick(c, "id", "Id")), name: pick(c, "name", "Name") || "Sem nome", typeId, income: typeId === TYPE_IDS.income,
    system: !!pick(c, "isSystem", "IsSystem"),          // categoria base do sistema (vale para todos)
    canEdit: pick(c, "canEdit", "CanEdit") !== false     // a API diz se o usuário logado pode alterar
  };
};
function normValue(v) {
  const type = pick(v, "valueType", "ValueType") || {};
  const typeId = Number(pick(v, "valueTypeId", "ValueTypeId") ?? pick(type, "id", "Id"));
  const typeName = String(pick(type, "name", "Name") || "").toLowerCase();
  const catId = Number(pick(v, "categoryId", "CategoryId"));
  const cat = pick(v, "category", "Category");
  const income = typeName ? typeName.includes("receit") || typeName.includes("entrad") : typeId === TYPE_IDS.income;
  return {
    id: Number(pick(v, "id", "Id")),
    name: pick(v, "name", "Name") || "Transação",
    value: Number(pick(v, "value", "Value")) || 0,
    date: parseDate(pick(v, "createWhen", "CreateWhen", "createdAt")),
    income,
    categoryId: catId,
    category: pick(cat || {}, "name", "Name") || categories.find(c => c.id === catId)?.name || "Sem categoria"
  };
}
function normGoal(g) {
  return {
    raw: g, // objeto original, usado ao editar para não perder campos (ex.: createdWhen)
    id: Number(pick(g, "id", "Id")),
    name: pick(g, "name", "Name") || "Meta",
    target: Number(pick(g, "targetValue", "TargetValue")) || 0,
    saved: Number(pick(g, "currentValue", "CurrentValue")) || 0,
    monthly: !!pick(g, "isMonthly", "IsMonthly"),
    priority: !!pick(g, "isPriority", "IsPriority"),
    endDate: parseDate(pick(g, "endDate", "EndDate"))
  };
}
const goalPct = (g) => g.target > 0 ? Math.min(100, Math.round(g.saved / g.target * 100)) : 0;

/* ---------- Carregamento ---------- */
const apiDown = (e) => showStatus(`${e.message}. Confirme se a API está rodando e se o CORS permite esta origem.`);
async function load() {
  const [cats, vals, gls] = await Promise.all([
    api(ENDPOINTS.categories).catch(e => { apiDown(e); return []; }),
    api(ENDPOINTS.values).catch(e => { apiDown(e); return []; }),
    api(ENDPOINTS.goals).catch(() => null)
  ]);
  categories = list(cats).map(normCategory);
  goals = gls === null ? null : list(gls).map(normGoal);
  values = list(vals).map(normValue).sort(byDateDesc);
  renderAll();
}
async function reloadValues() { values = list(await api(ENDPOINTS.values)).map(normValue).sort(byDateDesc); }
async function reloadCategories() { categories = list(await api(ENDPOINTS.categories)).map(normCategory); }
async function reloadGoals() { goals = list(await api(ENDPOINTS.goals)).map(normGoal); }

function renderAll() {
  renderCategorySelects(); renderChips(); renderGoal();
  renderRows(); renderGoalList(); renderCategoryList(); renderHistory();
}

function showStatus(msg) { const el = $("tx-status"); el.textContent = msg; el.classList.remove("hidden"); }
let toastTimer;
function toast(msg, error = false) {
  const t = $("toast"); t.textContent = msg; t.className = `toast${error ? " error" : ""}`;
  clearTimeout(toastTimer); toastTimer = setTimeout(() => t.classList.add("hidden"), 3500);
}

/* ---------- Abas ---------- */
function showView(viewId) {
  document.querySelectorAll(".tx-view").forEach(v => v.classList.toggle("hidden", v.id !== viewId));
  document.querySelectorAll(".tx-tab").forEach(t => {
    const on = t.dataset.view === viewId;
    t.classList.toggle("active", on);
    if (on) history.replaceState(null, "", `${location.pathname}${location.search}#${t.dataset.hash}`);
  });
  window.dispatchEvent(new Event("fcf:tab")); // avisa o menu lateral para marcar o item certo
  window.scrollTo({ top: 0, behavior: "smooth" });
}
// Menu lateral muda só o "#" da página (ex.: #metas -> #historico): troca de aba sem recarregar.
window.addEventListener("hashchange", () => {
  const tab = document.querySelector(`.tx-tab[data-hash="${location.hash.slice(1)}"]`);
  if (tab) showView(tab.dataset.view);
});
document.querySelectorAll(".tx-tab").forEach(tab => tab.addEventListener("click", () => showView(tab.dataset.view)));
document.querySelectorAll("[data-go]").forEach(el => el.addEventListener("click", (e) => { e.preventDefault(); showView(el.dataset.go); }));

/* ---------- Utilitários de formulário ---------- */
// Campo de dinheiro: o usuário digita só números e o valor é formatado como R$.
function moneyField(input) {
  let cents = 0;
  const paint = () => { input.value = cents ? money(cents / 100) : ""; };
  input.addEventListener("input", () => { cents = Number(input.value.replace(/\D/g, "").slice(0, 12) || 0); paint(); });
  return {
    get value() { return cents / 100; },
    set value(v) { cents = Math.round((Number(v) || 0) * 100); paint(); }
  };
}
// Mostra as mensagens de erro de um formulário. Retorna true se não houver erros.
function showErrors(form, errors) {
  form.querySelectorAll(".field-error").forEach(e => {
    const msg = errors[e.dataset.errorFor] || "";
    e.textContent = msg;
    $(e.dataset.errorFor).closest(".field").classList.toggle("invalid", !!msg);
  });
  return Object.keys(errors).length === 0;
}
// Liga um grupo de botões tipo "Receita / Despesa" e chama onChange com o valor escolhido.
function toggleGroup(selector, attr, current) {
  document.querySelectorAll(selector).forEach(b => {
    const on = b.dataset[attr] === current;
    b.classList.toggle("active", on);
    if (b.hasAttribute("aria-pressed")) b.setAttribute("aria-pressed", String(on));
    if (b.hasAttribute("aria-selected")) b.setAttribute("aria-selected", String(on));
  });
}
const errorMsg = (err, fallback) => err.friendly || fallback;

/* =====================================================================
   ABA NOVA TRANSAÇÃO
   ===================================================================== */
function renderCategorySelects() {
  const toOptions = (list) => list.map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join("");
  // No formulário, só as categorias do tipo escolhido (a API recusa receita em categoria de despesa).
  const ofType = categories.filter(c => c.typeId === TYPE_IDS[txType]);
  const keepTx = $("tx-category").value, keepH = $("h-category").value;
  $("tx-category").innerHTML = (ofType.length
    ? '<option value="">Selecione uma categoria</option>'
    : `<option value="">Nenhuma categoria de ${txType === "income" ? "receita" : "despesa"}</option>`) + toOptions(ofType);
  $("h-category").innerHTML = '<option value="">Todas as categorias</option>' + toOptions(categories);
  $("tx-category").value = keepTx; $("h-category").value = keepH;
}
const ICONS = [["moradia", "⌂"], ["casa", "⌂"], ["transporte", "🚗"], ["aliment", "🍴"], ["saúde", "♥"], ["saude", "♥"], ["mercado", "🛒"], ["lazer", "★"], ["educa", "✎"], ["salár", "＄"], ["salar", "＄"], ["invest", "↗"]];
const catIcon = (name) => ICONS.find(([k]) => name.toLowerCase().includes(k))?.[1] || "•";
const catColor = (id) => PALETTE[Math.max(0, categories.findIndex(c => c.id === id)) % PALETTE.length];

function renderChips() {
  const shown = categories.slice(0, 6);
  $("category-chips").innerHTML = shown.length ? shown.map(c =>
    `<div class="chip"><i style="background:${catColor(c.id)}">${catIcon(c.name)}</i>${esc(c.name)}</div>`).join("")
    : '<p class="empty-state">Nenhuma categoria cadastrada. Use “Nova Categoria”.</p>';
}
function renderGoal() {
  const box = $("goal-box");
  if (goals === null) { box.innerHTML = '<p class="empty-state">Não foi possível carregar as metas. Confirme se a API está rodando.</p>'; return; }
  const g = goals.find(x => (x.monthly ? "month" : "year") === goalScope);
  if (!g) { box.innerHTML = `<p class="empty-state">Nenhuma meta ${goalScope === "month" ? "do mês" : "do ano"} cadastrada.</p>`; return; }
  const pct = goalPct(g);
  box.innerHTML = `
    <div class="goal-top"><span class="gicon">▦</span><div><small>${esc(g.name)}</small><strong>${money(g.target)}</strong></div></div>
    <div class="goal-bar"><div class="progress-track"><span style="width:${pct}%"></span></div><b>${pct}%</b></div>
    <div class="goal-stats"><div><small>Já economizado</small><strong>${money(g.saved)}</strong></div><div><small>Faltam</small><strong>${money(Math.max(0, g.target - g.saved))}</strong></div></div>`;
}
function periodStart() {
  const p = $("period-filter").value; if (p === "all") return null;
  const d = new Date(); d.setDate(1); d.setHours(0, 0, 0, 0); d.setMonth(d.getMonth() - (Number(p) - 1)); return d;
}
const inPeriod = (v) => { const start = periodStart(); return !start || (v.date && v.date >= start); };
function rowCells(v) {
  return `<td>${fmtDate(v.date)}</td><td>${esc(v.name)}</td><td>${esc(v.category)}</td>
    <td><span class="badge ${v.income ? "income" : "expense"}">${v.income ? "Receita" : "Despesa"}</span></td>
    <td class="num ${v.income ? "val-income" : "val-expense"}">${v.income ? "+" : "−"} ${money(Math.abs(v.value))}</td>`;
}
// Botões ✎ editar e ✕ excluir de cada linha (usados nas duas tabelas).
const actionCells = (v) => `
  <td class="num">
    <button type="button" class="icon-btn" title="Editar" aria-label="Editar ${esc(v.name)}" data-edit-tx="${v.id}">✎</button>
    <button type="button" class="icon-btn danger" title="Excluir" aria-label="Excluir ${esc(v.name)}" data-del-tx="${v.id}">✕</button>
  </td>`;
function renderRows() {
  const rows = values.filter(inPeriod).slice(0, 5);
  $("tx-rows").innerHTML = rows.length ? rows.map(v => `<tr>${rowCells(v)}${actionCells(v)}</tr>`).join("")
    : '<tr><td colspan="6" class="empty-state">Nenhuma movimentação neste período.</td></tr>';
}

// Exclui uma receita/despesa (pede confirmação antes).
async function deleteTransaction(id) {
  const v = values.find(x => x.id === id); if (!v) return;
  const tipo = v.income ? "a receita" : "a despesa";
  if (!confirm(`Excluir ${tipo} "${v.name}" (${money(Math.abs(v.value))}) de ${fmtDate(v.date)}?\nEssa ação não pode ser desfeita.`)) return;
  try {
    await api(`${ENDPOINTS.values}/${v.id}`, { method: "DELETE" });
    if (editingTxId === v.id) resetForm();
    await reloadValues(); renderRows(); renderHistory(); renderCategoryList();
    toast(v.income ? "Receita excluída." : "Despesa excluída.");
  } catch (err) { console.error(err); toast(errorMsg(err, "Não foi possível excluir. Verifique a API e tente de novo."), true); }
}
// Cliques nos botões ✎ / ✕ das tabelas (Últimas Movimentações e Histórico).
function onTableClick(e) {
  const edit = e.target.closest("[data-edit-tx]");
  if (edit) return editTransaction(Number(edit.dataset.editTx));
  const del = e.target.closest("[data-del-tx]");
  if (del) deleteTransaction(Number(del.dataset.delTx));
}
$("tx-rows").addEventListener("click", onTableClick);

function setType(type) { txType = type; toggleGroup(".type-btn[data-type]", "type", type); renderCategorySelects(); }
const txValue = moneyField($("tx-value"));
function resetForm() {
  editingTxId = null;
  txValue.value = 0; $("tx-category").value = ""; $("tx-description").value = "";
  $("tx-date").value = toInputDate(new Date());
  $("tx-form-title").textContent = "Cadastrar";
  $("tx-form-subtitle").textContent = "Adicione uma nova receita ou despesa";
  $("tx-save").textContent = "✓ Salvar Transação";
  $("tx-clear").textContent = "↻ Limpar";
  $("tx-delete").classList.add("hidden");
  showErrors($("tx-form"), {});
}
function editTransaction(id) {
  const v = values.find(x => x.id === id); if (!v) return;
  resetForm();
  editingTxId = id;
  setType(v.income ? "income" : "expense");
  txValue.value = Math.abs(v.value);
  $("tx-category").value = String(v.categoryId);
  $("tx-date").value = toInputDate(v.date);
  $("tx-description").value = v.name;
  $("tx-form-title").textContent = "Editar transação";
  $("tx-form-subtitle").textContent = "Altere os dados e salve";
  $("tx-save").textContent = "✓ Atualizar Transação";
  $("tx-clear").textContent = "✕ Cancelar edição";
  $("tx-delete").textContent = `✕ Excluir esta ${v.income ? "receita" : "despesa"}`;
  $("tx-delete").classList.remove("hidden");
  showView("view-form");
}
$("tx-delete").addEventListener("click", () => { if (editingTxId) deleteTransaction(editingTxId); });
$("tx-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const errors = {};
  if (txValue.value <= 0) errors["tx-value"] = "Informe um valor maior que zero.";
  if (!$("tx-category").value) errors["tx-category"] = "Selecione uma categoria.";
  if (!$("tx-date").value) errors["tx-date"] = "Informe a data.";
  if (!showErrors($("tx-form"), errors)) return;

  const catId = Number($("tx-category").value);
  const catName = categories.find(c => c.id === catId)?.name;
  const payload = {
    name: $("tx-description").value.trim() || catName || "Transação",
    value: txValue.value,
    createWhen: `${$("tx-date").value}T00:00:00`,
    categoryId: catId,
    valueTypeId: TYPE_IDS[txType]
  };
  const editing = editingTxId;
  const btn = $("tx-save"); btn.disabled = true;
  try {
    if (editing) await api(`${ENDPOINTS.values}/${editing}`, { method: "PUT", body: JSON.stringify({ id: editing, ...payload }) });
    else await api(ENDPOINTS.values, { method: "POST", body: JSON.stringify(payload) });
    toast(editing ? "Transação atualizada." : (txType === "income" ? "Receita salva." : "Despesa salva."));
    resetForm();
    await reloadValues();
    renderRows(); renderHistory(); renderCategoryList();
  } catch (err) {
    console.error(err); toast(errorMsg(err, "Não foi possível salvar. Verifique a API e tente de novo."), true);
  } finally { btn.disabled = false; }
});
$("tx-clear").addEventListener("click", resetForm);
document.querySelectorAll(".type-btn[data-type]").forEach(b => b.addEventListener("click", () => setType(b.dataset.type)));
document.querySelectorAll("[data-goal-scope]").forEach(p => p.addEventListener("click", () => {
  goalScope = p.dataset.goalScope; toggleGroup("[data-goal-scope]", "goalScope", goalScope); renderGoal();
}));
$("period-filter").addEventListener("change", () => { renderRows(); renderHistory(); });

/* =====================================================================
   ABA METAS
   ===================================================================== */
const goalTarget = moneyField($("goal-target"));
const goalCurrent = moneyField($("goal-current"));
function setGoalType(type) { goalType = type; toggleGroup("[data-goal-type]", "goalType", type); }

function renderGoalList() {
  const box = $("goal-list");
  if (goals === null) { box.innerHTML = '<p class="empty-state">Não foi possível carregar as metas. Confirme se a API está rodando.</p>'; $("goal-count").textContent = ""; return; }
  const items = goals.filter(g => goalFilter === "all" || (g.monthly ? "month" : "year") === goalFilter);
  $("goal-count").textContent = `${goals.length} ${goals.length === 1 ? "meta" : "metas"}`;
  box.innerHTML = items.length ? items.map(g => {
    const pct = goalPct(g);
    const when = `${g.monthly ? "Meta do mês" : "Meta do ano"}${g.endDate ? ` · até ${fmtDate(g.endDate)}` : ""}`;
    return `
      <div class="item-row${editingGoal?.id === g.id ? " editing" : ""}">
        <i style="background:${g.monthly ? "#19b86f" : "#269cf0"}">${g.monthly ? "▦" : "★"}</i>
        <div class="item-main">
          <strong>${esc(g.name)}${g.priority ? '<span class="tag-priority">★ Prioritária</span>' : ""}${pct >= 100 ? '<span class="tag-done">Concluída</span>' : ""}</strong>
          <small>${money(g.saved)} de ${money(g.target)} · ${when}</small>
          <div class="goal-bar"><div class="progress-track"><span style="width:${pct}%"></span></div><b>${pct}%</b></div>
        </div>
        <div class="item-actions">
          <button type="button" class="icon-btn priority${g.priority ? " on" : ""}" title="${g.priority ? "Remover prioridade" : "Definir como meta prioritária"}" aria-label="${g.priority ? "Remover prioridade de" : "Definir como prioritária:"} ${esc(g.name)}" aria-pressed="${g.priority}" data-priority-goal="${g.id}">${g.priority ? "★" : "☆"}</button>
          <button type="button" class="icon-btn" title="Editar" aria-label="Editar ${esc(g.name)}" data-edit-goal="${g.id}">✎</button>
          <button type="button" class="icon-btn danger" title="Excluir" aria-label="Excluir ${esc(g.name)}" data-del-goal="${g.id}">✕</button>
        </div>
      </div>`;
  }).join("") : '<p class="empty-state">Nenhuma meta aqui ainda. Cadastre a primeira ao lado.</p>';
}
function resetGoalForm() {
  editingGoal = null;
  $("goal-name").value = ""; goalTarget.value = 0; goalCurrent.value = 0; $("goal-end").value = ""; $("goal-priority").checked = false;
  setGoalType("month");
  $("goal-form-title").textContent = "Nova Meta";
  $("goal-save").textContent = "✓ Salvar Meta";
  $("goal-clear").textContent = "↻ Limpar";
  showErrors($("goal-form"), {});
  renderGoalList();
}
function editGoal(id) {
  const g = goals.find(x => x.id === id); if (!g) return;
  resetGoalForm();
  editingGoal = g;
  $("goal-name").value = g.name; goalTarget.value = g.target; goalCurrent.value = g.saved;
  $("goal-end").value = toInputDate(g.endDate);
  $("goal-priority").checked = g.priority;
  setGoalType(g.monthly ? "month" : "year");
  $("goal-form-title").textContent = "Editar Meta";
  $("goal-save").textContent = "✓ Atualizar Meta";
  $("goal-clear").textContent = "✕ Cancelar edição";
  renderGoalList();
  $("goal-name").focus();
}
$("goal-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const errors = {};
  if (!$("goal-name").value.trim()) errors["goal-name"] = "Dê um nome para a meta.";
  if (goalTarget.value <= 0) errors["goal-target"] = "Informe um valor maior que zero.";
  if (!showErrors($("goal-form"), errors)) return;

  const now = new Date();
  const payload = {
    name: $("goal-name").value.trim(),
    targetValue: goalTarget.value,
    currentValue: goalCurrent.value,
    isMonthly: goalType === "month",
    isPriority: $("goal-priority").checked,   // a API tira a prioridade da meta anterior
    endDate: $("goal-end").value ? `${$("goal-end").value}T00:00:00` : null,
    // Mês de referência: primeiro dia do mês atual (mantém o original ao editar).
    referenceDate: pick(editingGoal?.raw, "referenceDate", "ReferenceDate") ?? `${toInputDate(new Date(now.getFullYear(), now.getMonth(), 1))}T00:00:00`
  };
  const editing = editingGoal;
  if (editing) { payload.id = editing.id; payload.createdWhen = pick(editing.raw, "createdWhen", "CreatedWhen"); }
  const btn = $("goal-save"); btn.disabled = true;
  try {
    if (editing) await api(`${ENDPOINTS.goals}/${editing.id}`, { method: "PUT", body: JSON.stringify(payload) });
    else await api(ENDPOINTS.goals, { method: "POST", body: JSON.stringify(payload) });
    toast(editing ? "Meta atualizada." : "Meta criada.");
    await reloadGoals();
    resetGoalForm(); renderGoal();
  } catch (err) {
    console.error(err); toast(errorMsg(err, "Não foi possível salvar a meta."), true);
  } finally { btn.disabled = false; }
});
$("goal-clear").addEventListener("click", resetGoalForm);
document.querySelectorAll("[data-goal-type]").forEach(b => b.addEventListener("click", () => setGoalType(b.dataset.goalType)));
document.querySelectorAll("[data-goal-filter]").forEach(p => p.addEventListener("click", () => {
  goalFilter = p.dataset.goalFilter; toggleGroup("[data-goal-filter]", "goalFilter", goalFilter); renderGoalList();
}));
$("goal-list").addEventListener("click", async (e) => {
  // ☆ / ★: define ou remove a meta prioritária (só pode haver uma; a API cuida disso).
  const star = e.target.closest("[data-priority-goal]");
  if (star) {
    const g = goals.find(x => x.id === Number(star.dataset.priorityGoal)); if (!g) return;
    star.disabled = true;
    try {
      await api(`${ENDPOINTS.goals}/${g.id}/priority`, { method: g.priority ? "DELETE" : "PUT" });
      await reloadGoals();
      if (editingGoal) { editingGoal = goals.find(x => x.id === editingGoal.id) || null; $("goal-priority").checked = !!editingGoal?.priority; }
      renderGoalList();
      toast(g.priority ? "Prioridade removida." : `"${g.name}" agora é a meta prioritária.`);
    } catch (err) { console.error(err); toast(errorMsg(err, "Não foi possível alterar a meta prioritária."), true); star.disabled = false; }
    return;
  }
  const edit = e.target.closest("[data-edit-goal]");
  if (edit) return editGoal(Number(edit.dataset.editGoal));
  const del = e.target.closest("[data-del-goal]");
  if (!del) return;
  const g = goals.find(x => x.id === Number(del.dataset.delGoal));
  if (!g || !confirm(`Excluir a meta "${g.name}"? Essa ação não pode ser desfeita.`)) return;
  try {
    await api(`${ENDPOINTS.goals}/${g.id}`, { method: "DELETE" });
    if (editingGoal?.id === g.id) resetGoalForm();
    await reloadGoals(); renderGoalList(); renderGoal();
    toast("Meta excluída.");
  } catch (err) { console.error(err); toast(errorMsg(err, "Não foi possível excluir a meta."), true); }
});

/* =====================================================================
   ABA CATEGORIAS
   ===================================================================== */
function setCatType(type) { catType = type; toggleGroup("[data-cat-type]", "catType", type); }
function renderCategoryList() {
  const items = categories.filter(c => catFilter === "all" || (c.income ? "income" : "expense") === catFilter);
  $("cat-count").textContent = `${categories.length} ${categories.length === 1 ? "categoria" : "categorias"}`;
  $("cat-list").innerHTML = items.length ? items.map(c => {
    const used = values.filter(v => v.categoryId === c.id).length;
    return `
      <div class="item-row${editingCatId === c.id ? " editing" : ""}">
        <i style="background:${catColor(c.id)}">${catIcon(c.name)}</i>
        <div class="item-main">
          <strong>${esc(c.name)}</strong>
          <small><span class="badge ${c.income ? "income" : "expense"}">${c.income ? "Receita" : "Despesa"}</span>${c.system ? '&nbsp;<span class="badge system" title="Categoria padrão, disponível para todos os usuários">Sistema</span>' : ""}&nbsp; ${used} ${used === 1 ? "lançamento" : "lançamentos"}</small>
        </div>
        <div class="item-actions">${c.canEdit ? `
          <button type="button" class="icon-btn" title="Editar" aria-label="Editar ${esc(c.name)}" data-edit-cat="${c.id}">✎</button>
          <button type="button" class="icon-btn danger" title="Excluir" aria-label="Excluir ${esc(c.name)}" data-del-cat="${c.id}">✕</button>` : ""}
        </div>
      </div>`;
  }).join("") : '<p class="empty-state">Nenhuma categoria aqui ainda. Cadastre a primeira ao lado.</p>';
}
function resetCatForm() {
  editingCatId = null;
  $("cat-name").value = ""; setCatType("income");
  $("cat-form-title").textContent = "Nova Categoria";
  $("cat-save").textContent = "✓ Salvar Categoria";
  $("cat-clear").textContent = "↻ Limpar";
  showErrors($("cat-form"), {});
  renderCategoryList();
}
function editCategory(id) {
  const c = categories.find(x => x.id === id); if (!c || !c.canEdit) return;
  resetCatForm();
  editingCatId = id;
  $("cat-name").value = c.name; setCatType(c.income ? "income" : "expense");
  $("cat-form-title").textContent = "Editar Categoria";
  $("cat-save").textContent = "✓ Atualizar Categoria";
  $("cat-clear").textContent = "✕ Cancelar edição";
  renderCategoryList();
  $("cat-name").focus();
}
$("cat-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const name = $("cat-name").value.trim();
  const errors = {};
  if (!name) errors["cat-name"] = "Dê um nome para a categoria.";
  else if (categories.some(c => c.id !== editingCatId && c.name.toLowerCase() === name.toLowerCase()))
    errors["cat-name"] = "Já existe uma categoria com esse nome.";
  if (!showErrors($("cat-form"), errors)) return;

  // A API espera CategoryTypeId (1 = Receita, 2 = Despesa).
  const editing = editingCatId;
  const payload = { id: editing || 0, name, categoryTypeId: TYPE_IDS[catType] };
  const btn = $("cat-save"); btn.disabled = true;
  try {
    if (editing) await api(`${ENDPOINTS.categories}/${editing}`, { method: "PUT", body: JSON.stringify(payload) });
    else await api(ENDPOINTS.categories, { method: "POST", body: JSON.stringify(payload) });
    toast(editing ? "Categoria atualizada." : "Categoria criada.");
    await reloadCategories();
    if (editing) { await reloadValues(); renderRows(); renderHistory(); } // nome novo aparece no histórico
    resetCatForm(); renderCategorySelects(); renderChips();
  } catch (err) {
    console.error(err); toast(errorMsg(err, "Não foi possível salvar a categoria."), true);
  } finally { btn.disabled = false; }
});
$("cat-clear").addEventListener("click", resetCatForm);
document.querySelectorAll("[data-cat-type]").forEach(b => b.addEventListener("click", () => setCatType(b.dataset.catType)));
document.querySelectorAll("[data-cat-filter]").forEach(p => p.addEventListener("click", () => {
  catFilter = p.dataset.catFilter; toggleGroup("[data-cat-filter]", "catFilter", catFilter); renderCategoryList();
}));
$("cat-list").addEventListener("click", async (e) => {
  const edit = e.target.closest("[data-edit-cat]");
  if (edit) return editCategory(Number(edit.dataset.editCat));
  const del = e.target.closest("[data-del-cat]");
  if (!del) return;
  const c = categories.find(x => x.id === Number(del.dataset.delCat)); if (!c) return;
  const used = values.filter(v => v.categoryId === c.id).length;
  if (used) { toast(`"${c.name}" tem ${used} ${used === 1 ? "lançamento" : "lançamentos"}. Mude ou exclua esses lançamentos antes.`, true); return; }
  if (!confirm(`Excluir a categoria "${c.name}"? Essa ação não pode ser desfeita.`)) return;
  try {
    await api(`${ENDPOINTS.categories}/${c.id}`, { method: "DELETE" });
    if (editingCatId === c.id) resetCatForm();
    await reloadCategories(); renderCategoryList(); renderCategorySelects(); renderChips();
    toast("Categoria excluída.");
  } catch (err) { console.error(err); toast(errorMsg(err, "Não foi possível excluir a categoria."), true); }
});

/* =====================================================================
   ABA HISTÓRICO
   ===================================================================== */
function renderHistory() {
  const search = $("h-search").value.trim().toLowerCase();
  const type = $("h-type").value;
  const catId = Number($("h-category").value) || null;
  const rows = values.filter(v => inPeriod(v)
    && (type === "all" || (type === "income") === v.income)
    && (!catId || v.categoryId === catId)
    && (!search || v.name.toLowerCase().includes(search) || v.category.toLowerCase().includes(search)));

  const income = rows.filter(v => v.income).reduce((s, v) => s + Math.abs(v.value), 0);
  const expense = rows.filter(v => !v.income).reduce((s, v) => s + Math.abs(v.value), 0);
  $("h-income").textContent = money(income);
  $("h-expense").textContent = money(expense);
  $("h-balance").textContent = money(income - expense);
  $("h-balance").className = income - expense < 0 ? "val-expense" : "val-income";
  $("h-count").textContent = `${rows.length} ${rows.length === 1 ? "movimentação" : "movimentações"}`;

  $("h-rows").innerHTML = rows.length ? rows.map(v => `
    <tr>${rowCells(v)}${actionCells(v)}</tr>`).join("")
    : '<tr><td colspan="6" class="empty-state">Nenhuma movimentação encontrada com esses filtros.</td></tr>';
}
["h-search", "h-type", "h-category"].forEach(id => $(id).addEventListener("input", renderHistory));
$("h-rows").addEventListener("click", onTableClick);

/* ---------- Início ---------- */
setType(txType);
resetForm();
setGoalType("month");
setCatType("income");
const startTab = document.querySelector(`.tx-tab[data-hash="${location.hash.slice(1)}"]`);
if (startTab) showView(startTab.dataset.view);
load();
