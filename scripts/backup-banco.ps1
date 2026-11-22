<#
  Backup do banco FCFinanceDb (SQL Server instalado no computador ou num servidor próprio/VPS).
  Hospedagens gerenciadas (ex.: Azure SQL) já fazem backup automático: lá este script não é necessário.

  Uso:
    powershell -ExecutionPolicy Bypass -File scripts\backup-banco.ps1
    powershell -ExecutionPolicy Bypass -File scripts\backup-banco.ps1 -Pasta "D:\Backups\FCFinance" -ManterDias 30

  Para rodar todo dia sozinho: Agendador de Tarefas do Windows -> Criar Tarefa Básica -> Diariamente ->
  Programa: powershell.exe | Argumentos: -ExecutionPolicy Bypass -File "C:\caminho\scripts\backup-banco.ps1"

  IMPORTANTE: um backup só no mesmo disco do banco não protege contra perda do disco/servidor.
  Copie a pasta de backups para outro lugar (outro disco, nuvem) com frequência, e TESTE a restauração.
#>
param(
  [string]$Servidor = ".",
  [string]$Banco = "FCFinanceDb",
  [string]$Pasta = "C:\Backups\FCFinance",   # o serviço do SQL Server precisa ter permissão de escrita aqui
  [int]$ManterDias = 14
)
$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force $Pasta | Out-Null
$arquivo = Join-Path $Pasta ("{0}-{1:yyyyMMdd-HHmmss}.bak" -f $Banco, (Get-Date))

# COPY_ONLY: não interfere em outra rotina de backup que já exista. CHECKSUM: confere a integridade ao gravar.
$sql = "BACKUP DATABASE [$Banco] TO DISK = N'$arquivo' WITH COPY_ONLY, COMPRESSION, CHECKSUM, INIT, STATS = 25;"

$cn = New-Object System.Data.SqlClient.SqlConnection "Server=$Servidor;Database=master;Trusted_Connection=True;TrustServerCertificate=True;"
$cn.Open()
try {
  $cmd = $cn.CreateCommand(); $cmd.CommandTimeout = 0; $cmd.CommandText = $sql
  try { $cmd.ExecuteNonQuery() | Out-Null }
  catch {
    # SQL Server Express não aceita COMPRESSION: tenta de novo sem compressão.
    if ($_.Exception.Message -match "COMPRESSION") { $cmd.CommandText = $sql.Replace("COMPRESSION, ", ""); $cmd.ExecuteNonQuery() | Out-Null }
    else { throw }
  }
  # Confere se o arquivo de backup pode ser lido (não restaura nada).
  $cmd.CommandText = "RESTORE VERIFYONLY FROM DISK = N'$arquivo' WITH CHECKSUM;"
  $cmd.ExecuteNonQuery() | Out-Null
}
finally { $cn.Close() }

Write-Host "Backup criado e verificado: $arquivo" -ForegroundColor Green

# Remove backups mais antigos que $ManterDias dias.
Get-ChildItem $Pasta -Filter "$Banco-*.bak" |
  Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$ManterDias) } |
  ForEach-Object { Remove-Item $_.FullName -Confirm:$false; Write-Host "Removido (antigo): $($_.Name)" }
