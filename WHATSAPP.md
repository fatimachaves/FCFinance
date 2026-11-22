# Bot do WhatsApp — como configurar (grátis, para testes)

O bot permite registrar receitas e despesas conversando pelo WhatsApp:

```
Você: 45,90
Bot:  Valor: R$ 45,90 — É uma receita ou uma despesa? 1️⃣ Receita 2️⃣ Despesa
Você: 2
Bot:  Qual é a categoria? 1. Mercado  2. Saúde ...
Você: 1
Bot:  Quer adicionar uma descrição? Envie o texto, ou "não" para pular.
Você: Compras da semana
Bot:  ✅ Despesa registrada! 💰 R$ 45,90 🏷️ Mercado 📝 Compras da semana 📅 21/11/2026
```

Comandos: **cancelar** (para o registro atual) e **ajuda**. Sem resposta por 10 minutos, a conversa recomeça.

> **Quer só testar a conversa?** Não precisa configurar nada: no site, abra o menu do seu nome → **WhatsApp** e use o **Simulador**. É o mesmo bot, e os lançamentos são salvos de verdade.

---

## Como funciona

```
Celular ──WhatsApp──► Meta (Cloud API) ──webhook──► túnel (Dev Tunnels) ──► API FCFinance
                                                                            │  /api/WhatsApp/webhook
Celular ◄──WhatsApp── Meta (Cloud API) ◄──── resposta do bot ───────────────┘
```

- A **Meta Cloud API** é o WhatsApp oficial. No modo de teste ela é **grátis**: dá um número de teste para o bot e deixa você conversar com até **5 celulares** cadastrados.
- A Meta precisa chegar na API que roda no seu PC. Para isso usamos um **túnel** público e grátis (Dev Tunnels, do próprio Visual Studio).
- Cada usuário conecta o próprio WhatsApp com um **código de 6 números** gerado no site (vale 10 minutos). Assim ninguém registra lançamentos na conta de outra pessoa.

---

## Passo 1 — Criar o app na Meta

1. Acesse **developers.facebook.com** e entre com sua conta do Facebook. Se for a primeira vez, aceite os termos de desenvolvedor.
2. **Meus apps → Criar app**. Escolha o caso de uso de **WhatsApp** (tipo *Empresa/Business*) e siga o assistente.
3. No painel do app, abra **WhatsApp → Configuração da API** (*API Setup*). Lá você encontra:
   - **Número de teste** (é o número do bot);
   - **Identificação do número de telefone** (*Phone number ID*) → vai em `WhatsApp:PhoneNumberId`;
   - **Token de acesso temporário** (*Temporary access token*) → vai em `WhatsApp:AccessToken`.
     ⚠️ Esse token **vence em 24 horas**. Para testes mais longos, crie um *Usuário do sistema* no Business Manager e gere um token permanente.
4. Ainda nessa tela, em **Para** (*To*), adicione o **seu celular** e confirme com o código que chega por WhatsApp (até 5 números).
5. Em **Configurações do app → Básico**, copie a **Chave secreta do aplicativo** (*App Secret*) → vai em `WhatsApp:AppSecret`.

## Passo 2 — Guardar os segredos na API

Os tokens são senhas: ficam em **User Secrets** (fora do Git), nunca no `appsettings`.
No terminal, dentro da pasta `FCFinance` (a da API):

```powershell
dotnet user-secrets set "WhatsApp:VerifyToken"   "invente-um-texto-qualquer-123"
dotnet user-secrets set "WhatsApp:AppSecret"     "<App Secret do passo 1.5>"
dotnet user-secrets set "WhatsApp:AccessToken"   "<token do passo 1.3>"
dotnet user-secrets set "WhatsApp:PhoneNumberId" "<Phone number ID do passo 1.3>"
```

O `VerifyToken` é um texto que **você inventa**. Ele vai ser colado também no painel da Meta (passo 4).

## Passo 3 — Abrir um túnel para a API (Dev Tunnels)

1. No Visual Studio, clique na setinha ao lado do botão **Iniciar ▶** → **Dev Tunnels → Criar um túnel...**
2. Escolha: **Tipo: Persistente** e **Acesso: Público**. Dê um nome (ex.: `fcfinance`) e confirme.
3. Rode o projeto (F5, perfil **Novo Perfil**). Na janela **Saída** (*Output*) → **Dev Tunnels** aparece o endereço público da API, algo como:
   `https://abc123-7148.brs.devtunnels.ms`
4. Teste no navegador: `https://<seu-tunel>/swagger` deve abrir o Swagger da API.

> Alternativa: **ngrok** (também grátis): `ngrok http https://localhost:7148`.

## Passo 4 — Ligar o webhook na Meta

1. No painel do app: **WhatsApp → Configuração** (*Configuration*) → **Webhook → Editar**.
2. **URL de retorno de chamada** (*Callback URL*): `https://<seu-tunel>/api/WhatsApp/webhook`
3. **Token de verificação** (*Verify token*): o mesmo texto que você inventou em `WhatsApp:VerifyToken`.
4. Clique em **Verificar e salvar**. A API precisa estar rodando.
5. Em **Campos do webhook** (*Webhook fields*), clique em **Assinar** (*Subscribe*) no campo **messages**.

## Passo 5 — Testar

1. No site: menu do seu nome → **WhatsApp** → **Gerar código**.
2. No celular, abra a conversa com o **número de teste** da Meta e envie o código de 6 números.
3. O bot responde "✅ Pronto! Seu WhatsApp está conectado". A tela do site também atualiza sozinha.
4. Envie **45,90** e siga as perguntas. 🎉

---

## Problemas comuns

| Sintoma | Causa provável e solução |
|---|---|
| "Verificar e salvar" falha na Meta | A API não está rodando, o túnel não é **Público**, ou o `VerifyToken` está diferente nos dois lugares. |
| O bot não responde nada | Veja o log da API. *"assinatura inválida"* = `AppSecret` errado. *"falha ao enviar mensagem (401)"* = token vencido (24 h): gere outro. |
| O bot conecta/entende, mas as respostas **não chegam** no celular | Problema do **9º dígito** dos celulares do Brasil: a Meta aceita a mensagem com o 9, mas não entrega. A API já responde **sem o 9** (opção `WhatsApp:BrazilSendWithoutNinthDigit`, ligada por padrão). No modo de teste, cadastre na lista **Para** o seu número **sem o 9** depois do DDD (ex.: `+55 11 1234-5678`). |
| Erro **131030** no log | O número para onde a API responde não está na lista **Para** do passo 1.4. Para celulares do Brasil, cadastre o número **sem o 9** (veja a linha acima). |
| Na tela do site aparece "O WhatsApp de verdade ainda não foi configurado" | Falta algum dos 4 segredos do passo 2. O simulador funciona mesmo assim. |

## Segurança (já implementado)

- Toda mensagem recebida tem a **assinatura da Meta conferida** (`X-Hub-Signature-256` com o App Secret). Mensagens falsas são recusadas.
- O código de conexão vale **10 minutos**, e o telefone é **bloqueado por 1 hora** depois de 5 códigos errados.
- Um telefone só pode estar ligado a **uma** conta (e uma conta a um telefone). Para trocar, use **Desconectar** no site.
- A mesma mensagem nunca é processada duas vezes (a Meta às vezes reenvia).
- O bot usa as **mesmas regras** do site: só as categorias do usuário e do sistema, e o tipo da categoria igual ao do lançamento.
