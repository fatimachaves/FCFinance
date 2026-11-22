# Publicação do FCFinance (produção)

O sistema tem **3 partes**, que rodam separadas:

| Parte | Projeto | O que faz |
|---|---|---|
| **Site** | `FCFinance.Web` | Telas, login (cookie), cadastro, e-mails |
| **API** | `FCFinance` | Dados financeiros (o JavaScript do site chama a API com um token) |
| **Banco** | SQL Server | Um banco só (`FCFinanceDb`) para os dois |

Endereços sugeridos (depois de registrar o domínio): `app.SEUDOMINIO.com.br` (site) e `api.SEUDOMINIO.com.br` (API).

---

## 1. Configurações de produção (variáveis de ambiente)

Os arquivos `appsettings.Production.json` **não têm senhas** e vêm com `TROCAR-...` onde falta o endereço real.
Enquanto houver um `TROCAR`, o sistema **se recusa a iniciar** (de propósito).
Senhas e chaves entram como **variáveis de ambiente** na hospedagem (o `:` vira `__`):

### API (`FCFinance`)
| Variável | Valor |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | conexão do banco de produção (com usuário/senha, **nunca** a do seu PC) |
| `Jwt__Key` | chave secreta com 48+ caracteres aleatórios — **a mesma do site** |
| `Cors__Origins__0` | endereço do site, ex.: `https://app.seudominio.com.br` |

### Site (`FCFinance.Web`)
| Variável | Valor |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | a mesma conexão da API |
| `Jwt__Key` | **a mesma** chave da API |
| `Api__BaseUrl` | endereço da API, ex.: `https://api.seudominio.com.br` |
| `Email__SmtpUsuario` | usuário SMTP da Brevo (painel Brevo → SMTP & API) |
| `Email__SmtpSenha` | chave SMTP da Brevo |
| `Email__Remetente` | ex.: `nao-responda@seudominio.com.br` (domínio verificado na Brevo) |
| `UsuariosPadrao__Admin__Senha` | senha forte do **admin**, só na 1ª publicação (depois pode remover) |

> Gerar uma chave aleatória (PowerShell):
> `$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)`

`Proxy:ConfiarCabecalhos` já vem `true` em produção: a hospedagem fica na frente do sistema (HTTPS) e repassa o IP real
do cliente. **Se publicar SEM proxy na frente** (Kestrel exposto direto), troque para `false`.

---

## 2. Banco de dados

1. Crie o banco vazio na hospedagem.
2. Gere os scripts: `powershell -ExecutionPolicy Bypass -File scripts\gerar-sql-migrations.ps1`
3. Rode **na ordem**: `deploy\sql\1-site-login.sql` e depois `deploy\sql\2-api-dados.sql`.
   (Os scripts são idempotentes: em atualizações futuras, rode os novos do mesmo jeito; só aplicam o que falta.)
4. O site também aplica as migrations de login sozinho ao iniciar e cria os perfis e o usuário **admin**.
5. **Backup:** hospedagens gerenciadas (ex.: Azure SQL) fazem backup automático. Em servidor próprio, use
   `scripts\backup-banco.ps1` (agendado todo dia) e copie os backups para **outro lugar** (outro disco/nuvem).
   **Teste uma restauração** antes de ter clientes.

---

## 3. E-mail (Brevo) e domínio

1. Registre o domínio (ex.: `fcfinance.com.br` no **registro.br**).
2. Na Brevo: **Remetentes e domínios → Adicionar domínio**, e copie os registros **SPF, DKIM e DMARC** para o painel do domínio.
   Sem isso, os e-mails caem no spam.
3. Crie a chave SMTP (Brevo → SMTP & API) e coloque em `Email__SmtpUsuario` / `Email__SmtpSenha`.

---

## 4. Depois de publicar: conferir

- [ ] `https://api.../health` e `https://app.../health` respondem **Healthy**
- [ ] Criar conta → chega o e-mail de confirmação (fora do spam) → confirmar → entrar
- [ ] Esqueci minha senha → chega o e-mail → criar nova senha
- [ ] Ativar a verificação em duas etapas no **admin**
- [ ] Lançar uma receita/despesa e ver no Dashboard
- [ ] Testar no celular (menu ☰)
- [ ] Configurar um aviso de "site fora do ar" usando o `/health` (ex.: UptimeRobot, plano grátis)

## 5. Antes de abrir para clientes

- [ ] Termos de Uso e Política de Privacidade escritos/revisados por advogado (hoje são rascunhos)
- [ ] CNPJ e emissão de nota fiscal (contador)
- [ ] Cobrança (Fase 4: planos + gateway de pagamento)
- [ ] Backup testado (restaurar num banco de teste)
