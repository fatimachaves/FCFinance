<#
  Gera os scripts SQL para atualizar o banco de PRODUÇÃO (API e site).

  Os scripts são "idempotentes": conferem o que já foi aplicado e só executam o que falta,
  então podem rodar mais de uma vez sem estragar nada.

  Uso (na pasta raiz do projeto):
    powershell -ExecutionPolicy Bypass -File scripts\gerar-sql-migrations.ps1

  Resultado: pasta deploy\sql\ com
    1-site-login.sql   -> tabelas de login (AspNet*) e chaves de criptografia (projeto FCFinance.Web)
    2-api-dados.sql    -> tabelas financeiras (projeto FCFinance)
  Rode NESSA ORDEM no banco de produção (ex.: Azure Data Studio / SSMS), com um backup feito antes.
#>
$ErrorActionPreference = "Stop"
$raiz = Split-Path $PSScriptRoot -Parent
$saida = Join-Path $raiz "deploy\sql"
New-Item -ItemType Directory -Force $saida | Out-Null

Write-Host "Gerando script do site (login)..."
dotnet ef migrations script --idempotent --context AuthDbContext --configuration Release `
  --project (Join-Path $raiz "FCFinance.Web\FCFinance.Web.csproj") `
  --output (Join-Path $saida "1-site-login.sql")
if ($LASTEXITCODE -ne 0) { throw "Falha ao gerar o script do site." }

Write-Host "Gerando script da API (dados financeiros)..."
dotnet ef migrations script --idempotent --context AppDbContext --configuration Release `
  --project (Join-Path $raiz "FCFinance\FCFinance.csproj") `
  --output (Join-Path $saida "2-api-dados.sql")
if ($LASTEXITCODE -ne 0) { throw "Falha ao gerar o script da API." }

Write-Host ""
Write-Host "Pronto: $saida" -ForegroundColor Green
Write-Host "Faça um BACKUP antes e rode 1-site-login.sql e depois 2-api-dados.sql no banco de produção."
