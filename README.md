# VPN Manager (GlobalProtect 64-bit)

Herramienta 100% portable e independiente para la **Desinstalación / Limpieza Total** e **Instalación automática** de **GlobalProtect VPN (Palo Alto Networks)** en equipos corporativos Windows.

## Características Principales
- **Dos módulos directos**:
  1. **Desinstalar VPN (Limpieza Total)**: Desregistra servicios y drivers (`PanGPS`, `PanGpHip`, `pangpd`, `gpfltdrv`), limpia todas las carpetas de `Program Files`, `ProgramData` y `AppData` de todos los perfiles de usuario, elimina accesos directos y remueve el registro en el Panel de Control.
  2. **VPN (GlobalProtect 64-bit)**: Descarga/almacena en caché local `GlobalProtect64.msi`, despliega los binarios oficiales firmados, registra el programa en el Panel de Control y en el Menú Inicio de Windows, e inicia el servicio `PanGPS` y el cliente `PanGPA.exe`.
- **Ejecución Automática en Segundo Plano**:
  - Utiliza internamente la cuenta de soporte local configurada sin solicitar credenciales UAC al usuario final ni disparar bloqueos de EDR.
- **Portabilidad 100% Estricta**:
  - No deja carpetas sueltas en el Escritorio; almacena su caché e instalador dentro del contenedor oculto `.vpnmanager` en `Documentos`.

## Compilación
Ejecutar en PowerShell:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

