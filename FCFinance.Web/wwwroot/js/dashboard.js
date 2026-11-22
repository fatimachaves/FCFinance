/*
  FCFinance - Dashboard em HTML/CSS/JS puro.
  Ajuste API_BASE_URL e TYPE_IDS para corresponder à sua API.
*/
const API_BASE_URL = "https://localhost:7148/api";
const ENDPOINTS = {
  values: `${API_BASE_URL}/RegisteredValue`,
  categories: `${API_BASE_URL}/Category`,
  goals: `${API_BASE_URL}/Goals`
};

// IMP

const TYPE_IDS = { income: [1], expense: [2] };
const CURRENCY = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const COLORS = ["#20df80", "#269cf0", "#9650dc", "#ffa51e", "#8195b8", "#ed638a", "#19c7bb"];
let allValues = [];
let categories = [];
let goals = [];

const $ = (id) => document.getElementById(id);
const money = (value) => CURRENCY.format(Number(value) || 0);
const get = (obj, ...keys) => {
  for (const key of keys) if (obj && obj[key] !== undefined && obj[key] !== null) return obj[key];
  return undefined;
};
const parseDate = (value) => {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
};
const formatDate = (date) => date ? new Intl.DateTimeFormat("pt-BR").format(date) : "—";

function normalizeValue(item) {
  const category = get(item, "category", "Category") || {};
  const type = get(item, "valueType", "ValueType") || {};
  const typeId = Number(get(item, "valueTypeId", "ValueTypeId") ?? get(type, "id", "Id"));
  const categoryId = Number(get(item, "categoryId", "CategoryId") ?? get(category, "id", "Id"));
  const rawValue = Number(get(item, "value", "Value") ?? 0);
  return {
    id: get(item, "id", "Id"),
    name: get(item, "name", "Name") || "Transação",

    value: Number.isFinite(rawValue) ? rawValue : 0,
    date: parseDate(get(item, "createWhen", "CreateWhen", "createdAt", "CreatedAt")),
    categoryId,
    categoryName: get(category, "name", "Name") || categories.find(c => Number(c.id) === categoryId)?.name || "Sem categoria",
    typeId,
    typeName: String(get(type, "name", "Name", "type", "Type") || "").toLowerCase()
  };
}

function isIncome(item) {
  if (item.typeName.includes("receit") || item.typeName.includes("entrad")) return true;
  if (item.typeName.includes("despes") || item.typeName.includes("saíd") || item.typeName.includes("saida")) return false;
  return TYPE_IDS.income.includes(item.typeId);
}
function isExpense(item) {
  if (item.typeName.includes("despes") || item.typeName.includes("saíd") || item.typeName.includes("saida")) return true;
  if (item.typeName.includes("receit") || item.typeName.includes("entrad")) return false;
  return TYPE_IDS.expense.includes(item.typeId);
}

async function fetchJson(url) {
  const response = await fetch(url, { headers: { "Accept": "application/json" } });
  if (!response.ok) throw new Error(`Erro ${response.status} ao consultar ${url}. Verifique a rota da API.`);
  return response.json();
}

async function initializeDashboard() {
  $("loading").classList.remove("hidden");
  $("error-message").classList.add("hidden");
  try {
      // As duas rotas são consultadas em paralelo.
    const [valuesResponse, categoriesResponse, goalsResponse] = await Promise.all([
      fetchJson(ENDPOINTS.values),
      fetchJson(ENDPOINTS.categories).catch(() => []),
      fetchJson(ENDPOINTS.goals).catch(() => [])
    ]);
    const valuesArray = Array.isArray(valuesResponse) ? valuesResponse : (valuesResponse.data || valuesResponse.items || []);
    const categoriesArray = Array.isArray(categoriesResponse) ? categoriesResponse : (categoriesResponse.data || categoriesResponse.items || []);
    const goalsArray = Array.isArray(goalsResponse) ? goalsResponse : (goalsResponse.data || goalsResponse.items || []);
    categories = categoriesArray.map(c => ({
      id: get(c, "id", "Id"),
      name: get(c, "name", "Name") || "Sem categoria"
    }));
    goals = goalsArray.map(c => ({
        id: get(c, "id", "Id"),
        name: get(c, "name", "Name") || "Sem Meta",
        currentValue: get(c, "currentValue", "CurrentValue") || 0,
        targetValue: get(c, "targetValue", "TargetValue") || 0,
        isMonthly: !!get(c, "isMonthly", "IsMonthly"),
        isPriority: !!get(c, "isPriority", "IsPriority")
    }));

    allValues = valuesArray.map(normalizeValue).sort((a,b) => (b.date?.getTime() || 0) - (a.date?.getTime() || 0));
    renderDashboard();
  } catch (error) {
    console.error(error);
    $("error-message").textContent = `${error.message} Confirme se a API está em execução e se o CORS permite a origem desta página.`;
    $("error-message").classList.remove("hidden");
    renderDashboard();
  } finally {
    $("loading").classList.add("hidden");
  }
}

function getFilteredValues() {
  const period = $("period-filter").value;
  if (period === "all") return allValues;
  const months = Number(period);
  const start = new Date();
  start.setDate(1);
  start.setHours(0,0,0,0);
  start.setMonth(start.getMonth() - (months - 1));
  return allValues.filter(v => v.date && v.date >= start);
}

function renderDashboard() {
  const values = getFilteredValues();
  const incomes = values.filter(isIncome);
  const expenses = values.filter(isExpense);
  const incomeTotal = incomes.reduce((s,v) => s + v.value, 0);
  const expenseTotal = expenses.reduce((s,v) => s + v.value, 0);
  $("total-income").textContent = money(incomeTotal);
  $("total-expense").textContent = money(expenseTotal);
  $("total-balance").textContent = money(incomeTotal - expenseTotal);
  $("donut-total").textContent = money(expenseTotal).replace(",00", "");
  renderEvolution(values);
  renderCategories(expenses, expenseTotal);
  renderTransactions(values);
  renderGoals(goals);
  renderPriorityGoal(goals);
}

function getMonthBuckets(values) {
  const count = $("period-filter").value === "all" ? 6 : Number($("period-filter").value);
  const months = Math.min(Math.max(count, 1), 12);
  const now = new Date();
  const buckets = [];
  for (let i = months - 1; i >= 0; i--) {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
    buckets.push({ key: `${d.getFullYear()}-${d.getMonth()}`, label: d.toLocaleDateString("pt-BR", {month:"short"}).replace(".",""), income:0, expense:0 });
  }
  values.forEach(v => {
    if (!v.date) return;
    const key = `${v.date.getFullYear()}-${v.date.getMonth()}`;
    const bucket = buckets.find(b => b.key === key);
    if (bucket) {
      if (isIncome(v)) bucket.income += v.value;
      if (isExpense(v)) bucket.expense += v.value;
    }
  });
  return buckets;
}

function setupCanvas(canvas) {
  const rect = canvas.getBoundingClientRect();
  const ratio = window.devicePixelRatio || 1;
  canvas.width = Math.max(1, rect.width * ratio);
  canvas.height = Math.max(1, rect.height * ratio);
  const ctx = canvas.getContext("2d");
  ctx.scale(ratio, ratio);
  return { ctx, width: rect.width, height: rect.height };
}

function renderEvolution(values) {
  const canvas = $("evolution-chart");
  const {ctx, width:w, height:h} = setupCanvas(canvas);
  const buckets = getMonthBuckets(values);
  const maxValue = Math.max(1, ...buckets.flatMap(b => [b.income,b.expense]));
  const top = 16, bottom = 31, left = 42, right = 12;
  const chartH = h - top - bottom, chartW = w - left - right;
  ctx.clearRect(0,0,w,h);
  ctx.font = "12px DM Sans"; ctx.textAlign = "right"; ctx.fillStyle = "#dbe3ef"; ctx.strokeStyle = "#303b49"; ctx.lineWidth = 1;
  for (let i=0;i<=4;i++) {
    const y = top + chartH*i/4;
    ctx.beginPath();ctx.moveTo(left,y);ctx.lineTo(w-right,y);ctx.stroke();
    ctx.fillText(formatCompact(maxValue*(1-i/4)), left-8, y+4);
  }
  const xAt = i => left + (buckets.length===1 ? chartW/2 : chartW*i/(buckets.length-1));
  buckets.forEach((b,i) => {ctx.textAlign="center";ctx.fillText(b.label,xAt(i),h-7);});
  const drawLine = (key,color) => {
    const points = buckets.map((b,i)=>({x:xAt(i),y:top+chartH-(b[key]/maxValue)*chartH}));
    if (!points.length) return;
    ctx.beginPath();ctx.moveTo(points[0].x,points[0].y);points.slice(1).forEach(p=>ctx.lineTo(p.x,p.y));
    ctx.strokeStyle=color;ctx.lineWidth=3;ctx.lineJoin="round";ctx.stroke();
    points.forEach(p=>{ctx.beginPath();ctx.arc(p.x,p.y,5,0,Math.PI*2);ctx.fillStyle=color;ctx.fill();});
  };
  drawLine("income","#20df80");drawLine("expense","#ffa51e");
}
function formatCompact(v) { return v >= 1000 ? `${(v/1000).toFixed(v%1000===0?0:1)}k` : Math.round(v).toString(); }

function renderCategories(expenses, total) {
  const canvas = $("category-chart");
  const {ctx,width:w,height:h} = setupCanvas(canvas);
  const grouped = new Map();
  expenses.forEach(v => grouped.set(v.categoryName,(grouped.get(v.categoryName)||0)+v.value));
  const entries = [...grouped.entries()].sort((a,b)=>b[1]-a[1]).slice(0,7);
  ctx.clearRect(0,0,w,h);
  const cx=w/2,cy=h/2,r=Math.min(w,h)*.47,inner=r*.58;
  if (!entries.length || total <= 0) {
    ctx.beginPath();ctx.arc(cx,cy,r,0,Math.PI*2);ctx.arc(cx,cy,inner,0,Math.PI*2,true);ctx.fillStyle="#344052";ctx.fill();
  } else {
    let angle=-Math.PI/2;
    entries.forEach(([name,value],i)=>{
      const next=angle+(value/total)*Math.PI*2;
      ctx.beginPath();ctx.arc(cx,cy,r,angle,next);ctx.arc(cx,cy,inner,next,angle,true);ctx.closePath();ctx.fillStyle=COLORS[i%COLORS.length];ctx.fill();angle=next;
    });
  }
  $("category-legend").innerHTML = entries.length ? entries.map(([name,value],i)=>`
    <div class="category-item"><i class="category-color" style="background:${COLORS[i%COLORS.length]}"></i><span title="${escapeHtml(name)}">${escapeHtml(name)}</span><b>${total ? Math.round(value/total*100) : 0}%</b></div>`).join("") : '<p class="empty-state">Sem despesas categorizadas no período.</p>';
}
function renderTransactions(values) {
  const latest = values.slice(0,5);
  $("transactions-list").innerHTML = latest.length ? latest.map(v=>{
    const income=isIncome(v), sign=income?"+":"−";
    return `<div class="transaction"><span class="transaction-icon ${income?"income":"expense"}">${income?"↑":"−"}</span><span>${escapeHtml(v.name)}</span><time class="transaction-date">${formatDate(v.date)}</time><strong class="transaction-value ${income?"income":"expense"}">${sign} ${money(Math.abs(v.value))}</strong></div>`;
  }).join("") : '<p class="empty-state">Nenhuma transação encontrada para este período.</p>';
}


// Percentual de progresso da meta (protege contra divisão por zero).
const goalProgress = (goal) => goal.targetValue > 0 ? (goal.currentValue / goal.targetValue) * 100 : 0;

// Card "Meta Prioritária" (ao lado do Saldo do Período). Só existe UMA meta prioritária,
// escolhida pelo usuário na tela de Metas (botão ☆).
function renderPriorityGoal(goals) {
    const goal = goals.find(g => g.isPriority);
    const name = $("priority-goal-name");
    const box = $("priority-goal");
    if (!name || !box) return; // página antiga em cache: não quebra o resto do dashboard
    if (!goal) {
        name.textContent = "—";
        box.innerHTML = `<p class="empty-state">Nenhuma meta prioritária. <a href="/Transacoes#metas">Definir agora</a></p>`;
        return;
    }
    const progress = goalProgress(goal);
    name.textContent = goal.name;
    name.title = goal.name;
    box.innerHTML = `
        <div class="priority-line">
            <div class="progress-track"><span style="width: ${Math.min(100, progress)}%"></span></div>
            <span class="goal-percent">${progress.toFixed(2)}%</span>
        </div>
        <small>${formatCurrency(goal.currentValue)} de ${formatCurrency(goal.targetValue)}</small>`;
}
function renderGoals(goals) {
    let html = "";

    goals.forEach(goal => {
        let progress = goalProgress(goal);

    html += `
            <div class="goal-row">
                <div class="goal-emoji">🐷</div>

                <div class="goal-info">
                    <strong>${goal.isPriority ? "★ " : ""}${escapeHtml(goal.name)}</strong>

                    <div class="progress-track">
                        <span style="width: ${Math.min(100, progress)}%"></span>
                    </div>

                    <small>
                        ${formatCurrency(goal.currentValue)} /
                        ${formatCurrency(goal.targetValue)}
                    </small>
                </div>

                <span class="goal-percent">
                    ${progress.toFixed(2)}%
                </span>
            </div>
        `;
    });
    $("goals-list").innerHTML = html;
}

function formatCurrency(value) {
    return Number(value).toLocaleString("pt-BR", {
        style: "currency",
        currency: "BRL"
    });
}
function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, ch => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#039;"}[ch]));
}

$("period-filter").addEventListener("change", renderDashboard);

window.addEventListener("resize", () => { clearTimeout(window.__fcResize); window.__fcResize=setTimeout(renderDashboard,120); });
initializeDashboard();
