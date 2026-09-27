# ---------------------------------------------------------------------------
# Despliega en el servidor lo que haya en C:\inst\rapido.tgz
#
# EL PROBLEMA QUE RESUELVE: con la aplicacion corriendo, Windows mantiene los
# .dll BLOQUEADOS y la copia falla renglon por renglon ("Can't unlink
# already-existing object"). El primer intento detenia el pool y esperaba
# 700 ms fijos: a veces alcanzaba y a veces no, y cuando no, quedaba una mezcla
# de archivos nuevos y viejos — y el script decia que todo habia ido bien.
#
# LA SOLUCION NO ES ESPERAR MAS, es usar el mecanismo que ASP.NET Core trae
# para esto: app_offline.htm. Al aparecer ese archivo, el modulo de IIS apaga
# la aplicacion ordenadamente y SUELTA los archivos. Al borrarlo, arranca sola.
# Es lo que existe justamente para desplegar sin pelearse con los bloqueos.
# ---------------------------------------------------------------------------
<#
  Los sitios llegan como UN texto separado por comas y se parten aqui dentro.

  Parece un rodeo y es lo contrario: declarar el parametro como [string[]] y
  pasar "tauro zaragoza sac" por ssh se veia bien y solo enlazaba el PRIMERO —
  los otros dos se perdian en silencio y el despliegue quedaba a medias sin que
  nada protestara. Un solo argumento no se puede partir por accidente.
#>
param([string]$Sitios = 'tauro,zaragoza,sac')

$listaSitios = $Sitios.Split(',', [StringSplitOptions]::RemoveEmptyEntries) |
               ForEach-Object { $_.Trim() } | Where-Object { $_ }

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$paquete = 'C:\inst\rapido.tgz'
if (-not (Test-Path $paquete)) { throw "No esta $paquete" }

$fallos = @()

foreach ($s in $listaSitios) {
    $carpeta = "C:\apps\$s"
    $offline = Join-Path $carpeta 'app_offline.htm'

    try {
        'Se esta actualizando.' | Set-Content $offline -Encoding ascii

        # Se espera a que de verdad suelte los archivos, no un tiempo fijo:
        # se intenta renombrar el .dll principal, que es lo que estaria
        # bloqueado. En cuanto se puede, la aplicacion ya solto todo.
        $libre = $false
        $limite = (Get-Date).AddSeconds(30)
        $dll = Join-Path $carpeta 'ISL_Service.dll'
        while (-not $libre -and (Get-Date) -lt $limite) {
            try {
                $fs = [IO.File]::Open($dll, 'Open', 'ReadWrite', 'None')
                $fs.Close(); $libre = $true
            } catch { Start-Sleep -Milliseconds 300 }
        }
        <#
          SI NO SUELTA, EL PROBLEMA NO SIEMPRE ES QUE FALTE TIEMPO.

          Paso de verdad: el proceso de trabajo de un sitio se quedo colgado. Ni
          app_offline ni parar el pool lo movieron —se quedo en "Stopping" para
          siempre— y el despliegue fallaba una y otra vez sin decir por que.
          Esperar mas no habria servido: no estaba tardando, estaba trabado.

          Asi que cuando se acaba el plazo se busca el w3wp DE ESTE POOL y se le
          termina. No es lo mismo que "reiniciar IIS": solo cae este sitio, los
          otros ni se enteran. Y se distingue por la linea de comandos, donde IIS
          escribe el nombre del pool.
        #>
        if (-not $libre) {
            $w3wp = Get-CimInstance Win32_Process -Filter "Name='w3wp.exe'" |
                    Where-Object { $_.CommandLine -like "*pool-$s*" }

            if ($w3wp) {
                "$s : el proceso no respondia, se termina el PID $($w3wp.ProcessId)"
                Stop-Process -Id $w3wp.ProcessId -Force -ErrorAction SilentlyContinue

                $limite2 = (Get-Date).AddSeconds(15)
                while (-not $libre -and (Get-Date) -lt $limite2) {
                    try {
                        $fs = [IO.File]::Open($dll, 'Open', 'ReadWrite', 'None')
                        $fs.Close(); $libre = $true
                    } catch { Start-Sleep -Milliseconds 300 }
                }
            }
        }

        if (-not $libre) { throw "la aplicacion no solto los archivos ni terminando su proceso" }

        # tar devuelve != 0 si algo fallo. ANTES no se miraba, y ahi estaba el
        # problema: un despliegue a medias se reportaba como bueno.
        $salida = & tar -xzf $paquete -C $carpeta 2>&1
        if ($LASTEXITCODE -ne 0) { throw "tar fallo: $salida" }

        # Prueba de que de verdad se copio: la fecha del binario tiene que ser
        # de hace un momento. Sin esto solo se confia en que tar no protesto.
        $edad = (Get-Date) - (Get-Item $dll).LastWriteTime
        if ($edad.TotalMinutes -gt 10) { throw "el binario no se actualizo (fecha: $((Get-Item $dll).LastWriteTime))" }

        "$s : actualizado"
    }
    catch {
        $fallos += "$s : $($_.Exception.Message)"
        "$s : FALLO - $($_.Exception.Message)"
    }
    finally {
        # Pase lo que pase se quita el app_offline: dejarlo puesto deja el
        # sitio caido, y eso si seria peor que un despliegue fallido.
        Remove-Item $offline -Force -ErrorAction SilentlyContinue
    }
}

if ($fallos.Count -gt 0) {
    # Se dice CUALES fallaron y no "fallo el despliegue": los demas si quedaron
    # actualizados, y dar a entender lo contrario manda a revisar lo que ya esta
    # bien mientras lo roto sigue roto.
    Write-Error ("Siguen con la version anterior: " + ($fallos -join ' | '))
    exit 1
}
