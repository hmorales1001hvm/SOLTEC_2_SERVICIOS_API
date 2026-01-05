@echo off

REM Name of the service
set "serviceName=Soltec SFSPOS"

REM Description for the service
set "ServiceDescription=Servicio de transmicion Soltec Consultores"

REM Rootpath from the zip file
set "zipFile=C:\sfspos\serviceUpdate\service.zip"

REM Final directory
set "destDir=C:\sfspos"

REM Roothpath from the execute program.
set "serviceExe=C:\sfspos\service\Soltec.ServicioTransmision.exe"

REM Win rar path exutable
set "winrarPath=C:\Program Files\WinRAR\WinRAR.exe"

setlocal enabledelayedexpansion

set taskname=TareaProgramadaSoltec
set batfile=C:\sfspos\service\ReiniciaServicioTransmision.bat

:: Create a file VBS to execute a file .bat in mode hide and as administrator.
echo Set WshShell = CreateObject("WScript.Shell") > "C:\sfspos\serviceUpdate\IniciaTareaServicioSoltec.vbs"
echo command = "cmd.exe /c start /b """" ""%batfile%""" >> "C:\sfspos\serviceUpdate\IniciaTareaServicioSoltec.vbs"
echo WshShell.Run command, 0, True >> "C:\sfspos\serviceUpdate\IniciaTareaServicioSoltec.vbs"

:: Create a task on windows
schtasks /create /tn "%taskname%" /tr "wscript.exe C:\sfspos\serviceUpdate\IniciaTareaServicioSoltec.vbs" /sc daily /st 08:30 /rl highest /f

REM Check if the service exist.
sc query "%serviceName%" >nul 2>&1
if errorlevel 1 (
    echo El servicio "%serviceName%" no existe. Continuando con la descompresion...
    set "serviceExists=0"
) else (
    set "serviceExists=1"
)

REM Check the status from the service.
if "%serviceExists%"=="1" (

    echo Deteniendo el servicio "%serviceName%"...
	net stop "%serviceName%" >nul 2>&1
	
	REM If the servides is running, stoped
	:waitLoop
	sc query "%ServiceName%" | findstr /I /C:"STOPPED" >nul 2>&1
	if not errorlevel 1 (
		echo El servicio "%ServiceName%" se ha detenido.
		goto :decompress
	)
	
	echo Esperando el servicio "%ServiceName%" se ha detenido...
	timeout /t 6 /nobreak >nul
	goto waitLoop
)

REM Start process to decompress the zip file
:decompress
REM Check if the zip file exist.
if not exist "%zipFile%" (
    echo El archivo ZIP no se encuentra en la ruta especificada: %zipFile%
	
	echo Se inicia servicio "%serviceName%" existente nuevamente.
	goto :startService
	
    exit /b 1
)

REM Check if the destination path exist.
if not exist "%destDir%" (
    echo Creando el directorio de destino: %destDir%
    mkdir "%destDir%"
)

REM Unzip the zip file inside the destination path.
echo Descomprimiendo el archivo %zipFile% en %destDir%
ver | findstr /i "6.1" >nul
if %ERRORLEVEL% == 0 (
    echo /**** EL SISTEMA OPERATIVO ES WINDOWS 7. NO SE EJECUTARA EL COMANDO DE PowerShell ****/
	"%winrarPath%" x -ibck -y "%zipFile%" "%destDir%"
	if !errorlevel! equ 0 (
		echo Archivo descomprimido exitosamente.
	) else (
		echo Error al descomprimir el archivo.
	)
) else (
    powershell -command "Expand-Archive -Path '%zipFile%' -DestinationPath '%destDir%' -Force"
)

REM Delete the zip file after the unzip
echo Eliminando el archivo %zipFile%
del "%zipFile%" /f /q
echo Descompresion completada.

REM Create or start the service.
if "%serviceExists%"=="0" (
	goto :createService
	REM goto :startService
) else (
	echo Iniciando el servicio "%serviceName%"...
	goto :startService
)

:createService
echo Creando el serivicio "%ServiceName%"...
sc create "%ServiceName%" binPath= "C:\sfspos\service\Soltec.ServicioTransmision.exe" start= auto DisplayName= "%ServiceName%" >nul 2>&1
if %errorlevel% == 0 (
    echo Servicio "%ServiceName%" creado correctamente.

    REM add description to the service.
    sc description "%ServiceName%" "%ServiceDescription%" >nul 2>&1
    if %errorlevel% == 0 (
        echo Descripcion agreagada al servicio "%ServiceName%".
		
		REM Configure the service to restart automatically after 30 seconds in a fail case.
		sc failure "%serviceName%" reset= 0 actions= restart/30000/restart/30000/restart/30000
    ) else (
        echo Error al agregar la descripcion al servicio "%ServiceName%".
        exit /b 1
    )
	echo Iniciando el servicio "%serviceName%" recien creado...
    goto :startService
) else (
    echo Error al crear el servicio "%ServiceName%".
    exit /b 1
)

:startService
sc start "%serviceName%" >nul 2>&1
REM Check if the service started correctly
for /f "tokens=3 delims=: " %%H in ('sc query "%serviceName%" ^| findstr /C:"STATE"') do (
	set "serviceState=%%H"
)
if /i "%serviceState%"=="RUNNING" (
	echo El servicio "%serviceName%" se ha iniciado correctamente.
) else (
	echo Error: No se pudo iniciar el servicio "%serviceName%".
	exit /b 1
)

echo Operacion completada correctamente.
REM exit /b 1
REM pause
timeout /t 1 /nobreak >nul
