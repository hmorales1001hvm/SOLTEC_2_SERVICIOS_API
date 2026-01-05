@echo off
REM setlocal
setlocal enabledelayedexpansion

rem define a variables for the appTransmision
set appPath=C:\sfspos\serviceApp
set consoleAppName=Soltec.AppTransmision.exe
set zipAppTransmisionFile=C:\sfspos\serviceUpdate\serviceApp.zip

rem define a variables for the AppOnLineSales
set appPathAppOnLineSales=C:\sfspos\AppOnLineSales
set consoleAppNameAppOnLineSales=Soltec.AppOnLineSales.exe
set zipAppOnLineSalesFile=C:\sfspos\serviceUpdate\AppOnLineSales.zip

rem define a variables for the AppOnLineSales
set appPathSoltecCronApp=C:\sfspos\Soltec.Cron.App
set consoleSoltecCronApp=Soltec.Cron.App.exe
set zipSoltecCronAppFile=C:\sfspos\serviceUpdate\Soltec.Cron.App.zip

rem define a variable to descompress zip file
set destDir=C:\sfspos

set taskname=TareaProgramadaSoltec
set batfile=C:\sfspos\service\ReiniciaServicioTransmision.bat
set winrarPath=C:\Program Files\WinRAR\WinRAR.exe
set winrarPath86=C:\Program Files (x86)\WinRAR\WinRAR.exe


:: Create a file VBS to execute a file .bat in mode hide and as administrator.
echo Set WshShell = CreateObject("WScript.Shell") > "C:\sfspos\serviceUpdate\IniciaTareaServicioSoltec.vbs"
echo command = "cmd.exe /c start /b """" ""%batfile%""" >> "C:\sfspos\serviceUpdate\IniciaTareaServicioSoltec.vbs"
echo WshShell.Run command, 0, True >> "C:\sfspos\serviceUpdate\IniciaTareaServicioSoltec.vbs" 

:: Create a task on windows
schtasks /create /tn "%taskname%" /tr "wscript.exe C:\sfspos\serviceUpdate\IniciaTareaServicioSoltec.vbs" /sc daily /st 08:30 /rl highest /f


:stopApp
REM Detener la aplicación de consola si está en ejecución
echo Verificando si la aplicacion %consoleAppName% esta en ejecucion...
tasklist /FI "IMAGENAME eq %consoleAppName%" | find /i "%consoleAppName%" > nul
if %ERRORLEVEL% == 0 (
    echo La aplicacion %consoleAppName% esta en ejecucion. Deteniendola...
    taskkill /f /im %consoleAppName%
    echo La aplicacion %consoleAppName% se ha detenido correctamente.
) else (
    echo La aplicacion %consoleAppName% no esta en ejecucion.
)

REM Detener la aplicación de consola si está en ejecución
echo Verificando si la aplicacion %consoleAppNameAppOnLineSales% esta en ejecucion...
tasklist /FI "IMAGENAME eq %consoleAppNameAppOnLineSales%" | find /i "%consoleAppNameAppOnLineSales%" > nul
if %ERRORLEVEL% == 0 (
    echo La aplicacion %consoleAppNameAppOnLineSales% esta en ejecucion. Deteniendola...
    taskkill /f /im %consoleAppNameAppOnLineSales%
    echo La aplicacion %consoleAppNameAppOnLineSales% se ha detenido correctamente.
) else (
    echo La aplicacion %consoleAppNameAppOnLineSales% no esta en ejecucion.
)


REM Detener la aplicación de consola si está en ejecución
echo Verificando si la aplicacion %consoleSoltecCronApp% esta en ejecucion...
tasklist /FI "IMAGENAME eq %consoleSoltecCronApp%" | find /i "%consoleSoltecCronApp%" > nul
if %ERRORLEVEL% == 0 (
    echo La aplicacion %consoleSoltecCronApp% esta en ejecucion. Deteniendola...
    taskkill /f /im %consoleSoltecCronApp%
    echo La aplicacion %consoleSoltecCronApp% se ha detenido correctamente.
) else (
    echo La aplicacion %consoleSoltecCronApp% no esta en ejecucion.
)

REM Descomprimir los archivos .zip
echo Descomprimiendo los archivos ZIP...


REM Check if the ZIP app transmision file exists.
echo Descomprimiendo el archivo %zipAppTransmisionFile% en %destDir%
ver | findstr /i "6.1" >nul
if %ERRORLEVEL% == 0 (
    echo /**** EL SISTEMA OPERATIVO ES WINDOWS 7. NO SE EJECUTARA EL COMANDO DE PowerShell ****/
	
	if exist "%winrarPath%" (
		if exist "%zipAppTransmisionFile%" (
			echo El archivo %zipAppTransmisionFile% existe, descomprimiendo en %destDir%...
			"%winrarPath%" x -ibck -y "%zipAppTransmisionFile%" "%destDir%"
			if !errorlevel! neq 0 (
				echo Error al descomprimir el archivo %zipAppTransmisionFile%.
				REM goto :startService
			)
			echo Descompresion exitosa, eliminando el archivo %zipAppTransmisionFile%...
			del "%zipAppTransmisionFile%"
			if !errorlevel! neq 0 (
				echo Error al eliminar el archivo %zipAppTransmisionFile%.
				REM goto :startService
			) else (
				echo Archivo %zipAppTransmisionFile% eliminado correctamente.
				REM goto :startService
			)
		) else (
			echo El archivo %zipAppTransmisionFile% no existe.
			REM goto :startService
		)
	) else (
		if exist "%zipAppTransmisionFile%" (
			echo El archivo %zipAppTransmisionFile% existe, descomprimiendo en %destDir%...
			"%winrarPath86%" x -ibck -y "%zipAppTransmisionFile%" "%destDir%"
			if !errorlevel! neq 0 (
				echo Error al descomprimir el archivo %zipAppTransmisionFile%.
				REM goto :startService
			)
			echo Descompresion exitosa, eliminando el archivo %zipAppTransmisionFile%...
			del "%zipAppTransmisionFile%"
			if !errorlevel! neq 0 (
				echo Error al eliminar el archivo %zipAppTransmisionFile%.
				REM goto :startService
			) else (
				echo Archivo %zipAppTransmisionFile% eliminado correctamente.
				REM goto :startService
			)
		) else (
			echo El archivo %zipAppTransmisionFile% no existe.
			REM goto :startService
		)
	)
	
	
) else (
	if exist "%zipAppTransmisionFile%" (
		echo El archivo %zipAppTransmisionFile% existe, descomprimiendo en %destDir%... usando power shell
		powershell -command "Expand-Archive -Path '%zipAppTransmisionFile%' -DestinationPath '%destDir%' -Force"
		
		if !errorlevel! neq 0 (
			echo Error al descomprimir el archivo %zipAppTransmisionFile%.
			REM goto :startService
		)
		echo Descompresion exitosa, eliminando el archivo %zipAppTransmisionFile%...
		del "%zipAppTransmisionFile%"
		if !errorlevel! neq 0 (
			echo Error al eliminar el archivo %zipAppTransmisionFile%.
			REM goto :startService
		) else (
			echo Archivo %zipAppTransmisionFile% eliminado correctamente.
			REM goto :startService
		)
	) else (
		echo El archivo %zipAppTransmisionFile% no existe.
		REM goto :startService
	)
)


REM Check if the ZIP AppOnLineSales file exists.
echo Descomprimiendo el archivo %zipAppOnLineSalesFile% en %destDir%
ver | findstr /i "6.1" >nul
if %ERRORLEVEL% == 0 (
    echo /**** EL SISTEMA OPERATIVO ES WINDOWS 7. NO SE EJECUTARA EL COMANDO DE PowerShell ****/
	
	if exist "%winrarPath%" (
		if exist "%zipAppOnLineSalesFile%" (
			echo El archivo %zipAppOnLineSalesFile% existe, descomprimiendo en %destDir%...
			"%winrarPath%" x -ibck -y "%zipAppOnLineSalesFile%" "%destDir%"
			if !errorlevel! neq 0 (
				echo Error al descomprimir el archivo %zipAppOnLineSalesFile%.
				REM goto :startService
			)
			echo Descompresion exitosa, eliminando el archivo %zipAppOnLineSalesFile%...
			del "%zipAppOnLineSalesFile%"
			if !errorlevel! neq 0 (
				echo Error al eliminar el archivo %zipAppOnLineSalesFile%.
				REM goto :startService
			) else (
				echo Archivo %zipAppOnLineSalesFile% eliminado correctamente.
				REM goto :startService
			)
		) else (
			echo El archivo %zipAppOnLineSalesFile% no existe.
			REM goto :startService
		)
	) else (
		if exist "%zipAppOnLineSalesFile%" (
			echo El archivo %zipAppOnLineSalesFile% existe, descomprimiendo en %destDir%...
			"%winrarPath86%" x -ibck -y "%zipAppOnLineSalesFile%" "%destDir%"
			if !errorlevel! neq 0 (
				echo Error al descomprimir el archivo %zipAppOnLineSalesFile%.
				REM goto :startService
			)
			echo Descompresion exitosa, eliminando el archivo %zipAppOnLineSalesFile%...
			del "%zipAppOnLineSalesFile%"
			if !errorlevel! neq 0 (
				echo Error al eliminar el archivo %zipAppOnLineSalesFile%.
				REM goto :startService
			) else (
				echo Archivo %zipAppOnLineSalesFile% eliminado correctamente.
				REM goto :startService
			)
		) else (
			echo El archivo %zipAppOnLineSalesFile% no existe.
			REM goto :startService
		)
	)
	
) else (
	if exist "%zipAppOnLineSalesFile%" (
		echo El archivo %zipAppOnLineSalesFile% existe, descomprimiendo en %destDir%... usando power shell
		powershell -command "Expand-Archive -Path '%zipAppOnLineSalesFile%' -DestinationPath '%destDir%' -Force"
		
		if !errorlevel! neq 0 (
			echo Error al descomprimir el archivo %zipAppOnLineSalesFile%.
			REM goto :startService
		)
		echo Descompresion exitosa, eliminando el archivo %zipAppOnLineSalesFile%...
		del "%zipAppOnLineSalesFile%"
		if !errorlevel! neq 0 (
			echo Error al eliminar el archivo %zipAppOnLineSalesFile%.
			REM goto :startService
		) else (
			echo Archivo %zipAppOnLineSalesFile% eliminado correctamente.
			REM goto :startService
		)
	) else (
		echo El archivo %zipAppOnLineSalesFile% no existe.
		REM goto :startService
	)
)



REM Check if the ZIP Soltec.Cron.App.zip file exists.
echo Descomprimiendo el archivo %zipSoltecCronAppFile% en %destDir%
ver | findstr /i "6.1" >nul
if %ERRORLEVEL% == 0 (
    echo /**** EL SISTEMA OPERATIVO ES WINDOWS 7. NO SE EJECUTARA EL COMANDO DE PowerShell ****/
	
	if exist "%winrarPath%" (
		if exist "%zipSoltecCronAppFile%" (
			echo El archivo %zipSoltecCronAppFile% existe, descomprimiendo en %destDir%...
			"%winrarPath%" x -ibck -y "%zipSoltecCronAppFile%" "%destDir%"
			if !errorlevel! neq 0 (
				echo Error al descomprimir el archivo %zipSoltecCronAppFile%.
				REM goto :startService
			)
			echo Descompresion exitosa, eliminando el archivo %zipSoltecCronAppFile%...
			del "%zipSoltecCronAppFile%"
			if !errorlevel! neq 0 (
				echo Error al eliminar el archivo %zipSoltecCronAppFile%.
				REM goto :startService
			) else (
				echo Archivo %zipSoltecCronAppFile% eliminado correctamente.
				REM goto :startService
			)
		) else (
			echo El archivo %zipSoltecCronAppFile% no existe.
			REM goto :startService
		)
	) else (
		if exist "%zipSoltecCronAppFile%" (
			echo El archivo %zipSoltecCronAppFile% existe, descomprimiendo en %destDir%...
			"%winrarPath86%" x -ibck -y "%zipSoltecCronAppFile%" "%destDir%"
			if !errorlevel! neq 0 (
				echo Error al descomprimir el archivo %zipSoltecCronAppFile%.
				REM goto :startService
			)
			echo Descompresion exitosa, eliminando el archivo %zipSoltecCronAppFile%...
			del "%zipSoltecCronAppFile%"
			if !errorlevel! neq 0 (
				echo Error al eliminar el archivo %zipSoltecCronAppFile%.
				REM goto :startService
			) else (
				echo Archivo %zipSoltecCronAppFile% eliminado correctamente.
				REM goto :startService
			)
		) else (
			echo El archivo %zipSoltecCronAppFile% no existe.
			REM goto :startService
		)
	)
	
) else (
	if exist "%zipSoltecCronAppFile%" (
		echo El archivo %zipSoltecCronAppFile% existe, descomprimiendo en %destDir%... usando power shell
		powershell -command "Expand-Archive -Path '%zipSoltecCronAppFile%' -DestinationPath '%destDir%' -Force"
		
		if !errorlevel! neq 0 (
			echo Error al descomprimir el archivo %zipSoltecCronAppFile%.
			REM goto :startService
		)
		echo Descompresion exitosa, eliminando el archivo %zipSoltecCronAppFile%...
		del "%zipSoltecCronAppFile%"
		if !errorlevel! neq 0 (
			echo Error al eliminar el archivo %zipSoltecCronAppFile%.
			REM goto :startService
		) else (
			echo Archivo %zipSoltecCronAppFile% eliminado correctamente.
			REM goto :startService
		)
	) else (
		echo El archivo %zipSoltecCronAppFile% no existe.
		REM goto :startService
	)
)


REM Iniciar la aplicación de consola
REM echo Iniciando la aplicación de consola %consoleAppName%...
REM pause
REM start "" "%appPath%\%consoleAppName%"

rem :end
echo Operacion completada correctamente.
endlocal
exit /b
