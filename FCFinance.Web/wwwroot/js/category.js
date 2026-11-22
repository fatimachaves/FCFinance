
const form = document.getElementById("categoryForm");
const table = document.getElementById("categoriesTable");
const message = document.getElementById("message");

const categoryId = document.getElementById("categoryId");
const nameInput = document.getElementById("name");
const typeInput = document.getElementById("categoryTypeId");

const saveButton = document.getElementById("saveButton");
const cancelButton = document.getElementById("cancelButton");
const formTitle = document.getElementById("formTitle");


// LISTAR CATEGORIAS
async function listCategories() {

    try {
        const response = await fetch(API_URL);

        if (!response.ok)
            throw new Error("Erro ao buscar categorias.");

        const categories = await response.json();

        table.innerHTML = "";

        categories.forEach(category => {

            const tr = document.createElement("tr");

            const id = document.createElement("td");
            id.textContent = category.id;

            const name = document.createElement("td");
            name.textContent = category.name;

            const type = document.createElement("td");
            type.textContent = category.categoryTypeId;

            const actions = document.createElement("td");

            const editButton = document.createElement("button");
            editButton.textContent = "Editar";
            editButton.className = "edit";
            editButton.addEventListener("click", () => {
                editCategory(category);
            });

            const deleteButton = document.createElement("button");
            deleteButton.textContent = "Excluir";
            deleteButton.className = "delete";
            deleteButton.addEventListener("click", () => {
                deleteCategory(category.id);
            });

            actions.append(editButton, deleteButton);

            tr.append(id, name, type, actions);

            table.appendChild(tr);
        });

    } catch (error) {
        showMessage(error.message);
    }
}


// CADASTRAR E ATUALIZAR
form.addEventListener("submit", async function(event) {
    debugger;
    event.preventDefault();

    const id = categoryId.value;

    const category = {
        name: nameInput.value,
        categoryTypeId: Number(typeInput.value)
    };

    try {

        let response;

        if (id) {

            // ATUALIZAR
            response = await fetch(`${API_URL}/${id}`, {
                method: "PUT",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify(category)
            });

        } else {

            // CADASTRAR
            response = await fetch(API_URL, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify(category)
            });
        }

        if (!response.ok) {
            const error = await response.text();
            throw new Error(error || "Erro ao salvar categoria.");
        }

        showMessage(
            id
                ? "Categoria atualizada com sucesso!"
                : "Categoria cadastrada com sucesso!"
        );

        clearForm();

        await listCategories();

    } catch (error) {
        showMessage(error.message);
    }
});


// PREPARAR EDIÇÃO
function editCategory(category) {

    categoryId.value = category.id;
    nameInput.value = category.name;
    typeInput.value = category.categoryTypeId;

    formTitle.textContent = "Atualizar categoria";
    saveButton.textContent = "Salvar alterações";

    cancelButton.hidden = false;

    window.scrollTo({
        top: 0,
        behavior: "smooth"
    });
}


// EXCLUIR CATEGORIA
async function deleteCategory(id) {

    const confirmed = confirm(
        "Deseja realmente excluir esta categoria?"
    );

    if (!confirmed)
        return;

    try {

        const response = await fetch(`${API_URL}/${id}`, {
            method: "DELETE"
        });

        if (!response.ok)
            throw new Error("Erro ao excluir categoria.");

        showMessage("Categoria excluída com sucesso!");

        await listCategories();

    } catch (error) {
        showMessage(error.message);
    }
}


// LIMPAR FORMULÁRIO
function clearForm() {

    form.reset();

    categoryId.value = "";

    formTitle.textContent = "Cadastrar categoria";
    saveButton.textContent = "Cadastrar";

    cancelButton.hidden = true;
}


// CANCELAR EDIÇÃO
cancelButton.addEventListener("click", clearForm);


// MENSAGENS
function showMessage(text) {
    message.textContent = text;
}


// CARREGAR CATEGORIAS AO ABRIR A PÁGINA
listCategories();
