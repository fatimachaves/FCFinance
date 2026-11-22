
const REGISTERED_VALUE_API_URL =
    "https://localhost:7148/api/RegisteredValue";

// A variável API_URL vem do api.js anterior
// Ela aponta para /api/Category.

const form = document.getElementById("registeredValueForm");

const table = document.getElementById("registeredValuesTable");

const message = document.getElementById("message");

const idInput = document.getElementById("registeredValueId");
const nameInput = document.getElementById("name");
const valueInput = document.getElementById("value");
const typeInput = document.getElementById("valueTypeId");
const categoryInput = document.getElementById("categoryId");
const dateInput = document.getElementById("createWhen");

const saveButton = document.getElementById("saveButton");
const cancelButton = document.getElementById("cancelButton");
const formTitle = document.getElementById("formTitle");


// CARREGAR CATEGORIAS NO SELECT
async function loadCategories() {

    try {

        const response = await fetch(API_URL);

        if (!response.ok)
            throw new Error("Erro ao carregar categorias.");

        const categories = await response.json();

        categoryInput.innerHTML =
            '<option value="">Selecione uma categoria</option>';

        categories.forEach(category => {

            const option = document.createElement("option");

            option.value = category.id;
            option.textContent = category.name;

            categoryInput.appendChild(option);
        });

    } catch (error) {

        showMessage(error.message);
    }
}


// LISTAR LANÇAMENTOS
async function listRegisteredValues() {

    try {

        const response = await fetch(REGISTERED_VALUE_API_URL);

        if (!response.ok)
            throw new Error("Erro ao buscar lançamentos.");

        const registeredValues = await response.json();

        table.innerHTML = "";

        registeredValues.forEach(item => {

            const tr = document.createElement("tr");

            const typeName =
                Number(item.valueTypeId) === 1
                    ? "Receita"
                    : Number(item.valueTypeId) === 2
                        ? "Despesa"
                        : `Tipo ${item.valueTypeId}`;

            const categoryName =
                item.category?.name ?? `Categoria ${item.categoryId}`;

            const formattedValue = Number(item.value).toLocaleString(
                "pt-BR",
                {
                    style: "currency",
                    currency: "BRL"
                }
            );

            const formattedDate = formatDate(item.createWhen);

            const columns = [
                item.id,
                item.name,
                formattedValue,
                typeName,
                categoryName,
                formattedDate
            ];

            columns.forEach(value => {

                const td = document.createElement("td");

                td.textContent = value;

                tr.appendChild(td);
            });

            const actions = document.createElement("td");

            const editButton = document.createElement("button");

            editButton.textContent = "Editar";
            editButton.className = "edit";

            editButton.addEventListener("click", () => {
                editRegisteredValue(item);
            });

            const deleteButton = document.createElement("button");

            deleteButton.textContent = "Excluir";
            deleteButton.className = "delete";

            deleteButton.addEventListener("click", () => {
                deleteRegisteredValue(item.id);
            });

            actions.append(editButton, deleteButton);

            tr.appendChild(actions);

            table.appendChild(tr);
        });

    } catch (error) {

        showMessage(error.message);
    }
}


// CADASTRAR E ATUALIZAR
form.addEventListener("submit", async function(event) {

    event.preventDefault();

    const id = idInput.value;

    const registeredValue = {

        name: nameInput.value,

        value: Number(valueInput.value),

        valueTypeId: Number(typeInput.value),

        categoryId: Number(categoryInput.value),

        createWhen: `${dateInput.value}T00:00:00`
    };

    try {

        let response;

        if (id) {

            // ATUALIZAR
            response = await fetch(
                `${REGISTERED_VALUE_API_URL}/${id}`,
                {
                    method: "PUT",

                    headers: {
                        "Content-Type": "application/json"
                    },

                    body: JSON.stringify(registeredValue)
                }
            );

        } else {

            // CADASTRAR
            response = await fetch(
                REGISTERED_VALUE_API_URL,
                {
                    method: "POST",

                    headers: {
                        "Content-Type": "application/json"
                    },

                    body: JSON.stringify(registeredValue)
                }
            );
        }

        if (!response.ok) {

            const error = await response.text();

            throw new Error(error || "Erro ao salvar lançamento.");
        }

        showMessage(
            id
                ? "Lançamento atualizado com sucesso!"
                : "Lançamento registrado com sucesso!"
        );

        clearForm();

        await listRegisteredValues();

    } catch (error) {

        showMessage(error.message);
    }
});


// PREPARAR EDIÇÃO
function editRegisteredValue(item) {

    idInput.value = item.id;

    nameInput.value = item.name;

    valueInput.value = item.value;

    typeInput.value = item.valueTypeId;

    categoryInput.value = item.categoryId;

    dateInput.value = item.createWhen.split("T")[0];

    formTitle.textContent = "Atualizar lançamento";

    saveButton.textContent = "Salvar alterações";

    cancelButton.hidden = false;

    window.scrollTo({
        top: 0,
        behavior: "smooth"
    });
}


// EXCLUIR LANÇAMENTO
async function deleteRegisteredValue(id) {

    const confirmed = confirm(
        "Deseja realmente excluir este lançamento?"
    );

    if (!confirmed)
        return;

    try {

        const response = await fetch(
            `${REGISTERED_VALUE_API_URL}/${id}`,
            {
                method: "DELETE"
            }
        );

        if (!response.ok)
            throw new Error("Erro ao excluir lançamento.");

        showMessage("Lançamento excluído com sucesso!");

        await listRegisteredValues();

    } catch (error) {

        showMessage(error.message);
    }
}


// LIMPAR FORMULÁRIO
function clearForm() {

    form.reset();

    idInput.value = "";

    formTitle.textContent = "Novo lançamento";

    saveButton.textContent = "Registrar lançamento";

    cancelButton.hidden = true;
}


// FORMATAR DATA
function formatDate(date) {

    if (!date)
        return "";

    const [year, month, day] = date.split("T")[0].split("-");

    return `${day}/${month}/${year}`;
}


// MENSAGEM
function showMessage(text) {

    message.textContent = text;
}


// CANCELAR EDIÇÃO
cancelButton.addEventListener("click", clearForm);


// INICIALIZAÇÃO DA PÁGINA
async function initializePage() {

    const today = new Date();

    dateInput.value = [
        today.getFullYear(),
        String(today.getMonth() + 1).padStart(2, "0"),
        String(today.getDate()).padStart(2, "0")
    ].join("-");

    await loadCategories();

    await listRegisteredValues();
}

initializePage();
