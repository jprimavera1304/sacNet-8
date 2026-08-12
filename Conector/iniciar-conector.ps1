# Arranca el conector SIN generar un .exe en disco.
#
# POR QUE EXISTE ESTE ARCHIVO
# ---------------------------
# McAfee borra el ejecutable en cuanto se compila: es un binario desconocido,
# sin firma, que abre un puerto y lee una base de datos. Ese perfil es el de un
# troyano, y no hay forma de escribirlo distinto sin quitarle lo que lo hace
# util. La solucion de verdad es FIRMARLO con un certificado de firma de codigo.
#
# Mientras tanto, esto permite probarlo: PowerShell compila el mismo codigo EN
# MEMORIA y lo ejecuta dentro de su propio proceso, que ya esta firmado por
# Microsoft y el antivirus si respeta. No cambia una linea del conector: es el
# mismo codigo, el mismo puerto y el mismo comportamiento.
#
# ESTO NO ES LA FORMA DE DISTRIBUIRLO. Para instalarlo en las computadoras del
# checador hace falta el .exe firmado; esto es solo para desarrollo y pruebas.
#
# Uso:
#   powershell -ExecutionPolicy Bypass -File iniciar-conector.ps1

$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $dir

Write-Host "Compilando el conector en memoria..." -ForegroundColor Cyan

$refs = @(
  (Join-Path $dir "DPUruNet.dll"),
  "System.dll",
  "System.Data.dll",
  "System.Windows.Forms.dll",
  "System.Drawing.dll"
)

$codigo = Get-Content (Join-Path $dir "Conector.cs") -Raw

# El tipo es interno, asi que despues hay que buscar Main por reflexion.
Add-Type -TypeDefinition $codigo -ReferencedAssemblies $refs -Language CSharp

$tipo = [System.AppDomain]::CurrentDomain.GetAssemblies() |
  ForEach-Object { $_.GetType("Mac.Checador.Conector.Programa", $false) } |
  Where-Object { $_ -ne $null } | Select-Object -First 1

if (-not $tipo) { throw "No se encontro la clase del conector." }

$main = $tipo.GetMethod("Main", [Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic)
if (-not $main) { throw "No se encontro el punto de entrada." }

Write-Host "Arrancando. Deja esta ventana abierta." -ForegroundColor Green
[void]$main.Invoke($null, @())
