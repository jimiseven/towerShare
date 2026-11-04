# TowerShare 📋📶

**TowerShare** es una solución de sincronización bidireccional y en tiempo real del portapapeles (texto) entre computadoras con **Windows 10/11** y dispositivos móviles con **Android 12, 13 y 14+** mediante **Bluetooth Classic (RFCOMM / SPP)**.

---

## 🚀 Características Principales

* **Windows ➔ Android (100% Automático):**  
  Al hacer `Ctrl + C` (o clic derecho "Copiar") en cualquier parte de Windows, el texto se envía inmediatamente al celular y se inyecta en el portapapeles de Android **sin requerir ninguna pulsación adicional** (utilizando la **Opción B: Permiso de Superposición / SYSTEM_ALERT_WINDOW**).
* **Android ➔ Windows (Vía Menú Compartir):**  
  Selecciona cualquier texto en cualquier app de Android, pulsa **"Compartir"** y elige **"Enviar a PC (TowerShare)"**. El texto aparecerá al instante en el portapapeles de tu PC listo para hacer `Ctrl + V`.
* **Sin Servidores en la Nube:**  
  La comunicación ocurre directamente entre tu PC y tu teléfono a través de Bluetooth local (canal RFCOMM privado). No depende de Wi-Fi, internet ni cuentas externas.
* **Control de Eco (Loop-Free):**  
  Ambos clientes recuerdan el último texto recibido para evitar bucles infinitos de retroalimentación.
* **Modo Fantasma (100% Invisible y Cero Notificaciones):**  
  La app de Windows puede operar de forma 100% invisible: al minimizar o cerrar con la 'X', **desaparece tanto de la barra de tareas como de la bandeja del sistema (flecha `^` junto al reloj)**. No interrumpe con ningún tipo de globo ni notificación emergente.
* **Auto-Conexión y Reapertura Inteligente:**  
  TowerShare recuerda permanentemente el último celular conectado y se reconecta automáticamente en segundo plano. Si necesitas volver a abrir la ventana de configuración, basta con hacer clic nuevamente en el acceso directo de TowerShare; el proceso invisible detectará la llamada y abrirá la pantalla de inmediato.
* **Instalador Autónomo (Self-Contained):**  
  Incluye `TowerShare_Setup.exe`, un instalador autónomo que instala la aplicación de forma permanente en tu usuario, crea los accesos directos, permite iniciar con Windows y luego puedes borrar el instalador sin problemas.

---

## 📁 Estructura del Proyecto

```text
towerShare/
├── .gitignore
├── README.md
├── gu_a_de_desarrollo.txt               # Guía base de requerimientos
├── windows-client/                     # Cliente Windows en C# (.NET 8 + WinRT)
│   ├── TowerShareWindows.sln           # Solución de Visual Studio
│   └── TowerShareWindows/
│       ├── TowerShareWindows.csproj    # Target: net8.0-windows10.0.19041.0
│       ├── Program.cs                  # Punto de entrada
│       ├── MainForm.cs                 # Interfaz, eventos y lógica de bandeja
│       ├── MainForm.Designer.cs        # Diseño visual WinForms
│       ├── BluetoothClient.cs          # Conexión RFCOMM WinRT nativa
│       ├── ClipboardMonitor.cs         # Hook nativo de portapapeles (Win32)
│       ├── Protocol.cs                 # Framing binario (longitud + UTF-8)
│       └── app.manifest                # Manifiesto DPI y compatibilidad Win10/11
└── android-server/                     # Servidor Android en Kotlin (API 26 a 34+)
    ├── build.gradle                    # Configuración raíz de Gradle
    ├── settings.gradle
    ├── gradle.properties
    └── app/
        ├── build.gradle                # Dependencias AndroidX y Corrutinas
        ├── src/main/
        │   ├── AndroidManifest.xml     # Permisos Bluetooth y Foreground Service
        │   ├── res/                    # Layouts, estilos e iconos
        │   └── java/com/towershare/app/
        │       ├── MainActivity.kt               # Control de servidor y permisos
        │       ├── BluetoothServerService.kt     # Foreground Service RFCOMM
        │       ├── TransparentCopyActivity.kt    # Inyector transparente de portapapeles (Opción B)
        │       ├── ShareActivity.kt              # Receptor de ACTION_SEND ("Compartir")
        │       └── Protocol.kt                   # Desempaquetador de tramas
```

---

## ⚙️ Requisitos Previos

### En la PC (Windows):
* Windows 10 (versión 19041 o superior) o Windows 11.
* Adaptador Bluetooth encendido.
* Visual Studio 2022 (con carga de trabajo *.NET Desktop Development*) o [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

### En el Celular (Android):
* Android 12, 13 o 14+ (compatible desde Android 8.0 Oreo).
* Bluetooth encendido.
* Android Studio (Hedgehog / Iguana o superior) con SDK 34.

---

## 🛠️ Instrucciones de Compilación y Configuración

### 1. Preparar la App de Android

1. Abre la carpeta `android-server` en **Android Studio**.
2. Deja que Gradle sincronice las dependencias del proyecto.
3. Conecta tu celular mediante USB (con depuración USB activa) o genera el APK:  
   * En Android Studio: Menú `Build` > `Build Bundle(s) / APK(s)` > `Build APK(s)`.
4. Instala y abre la app **TowerShare** en tu celular:
   * Acepta los permisos de **Bluetooth** y **Notificaciones**.
   * Pulsa el botón **"Autorizar 'Aparecer encima'"** y activa el interruptor para TowerShare (esto habilita la copia 100% automática cuando Windows envía texto).
   * Pulsa el botón **"Iniciar Servidor"**. Verás una notificación persistente indicando que está esperando conexión.

---

### 2. Preparar la App de Windows

1. Abre la solución `windows-client/TowerShareWindows.sln` en **Visual Studio 2022**.
2. Compila el proyecto en modo `Release` o `Debug`.  
   *(También puedes compilarlo por terminal ejecutando `dotnet build windows-client/TowerShareWindows/TowerShareWindows.csproj -c Release`)*.
3. Ejecuta la aplicación `TowerShareWindows.exe`.

---

## 📲 Modo de Uso Diario

1. **Emparejamiento (Solo la primera vez):**
   * Ve a `Configuración de Windows` > `Bluetooth y dispositivos` > `Agregar dispositivo`.
   * Empareja tu celular Android con la computadora.
2. **Iniciar la conexión:**
   * En el celular: Abre TowerShare y verifica que diga *"Servidor activo: esperando conexión"*.
   * En la PC: Abre TowerShare, selecciona tu celular en la lista desplegable y pulsa **"Conectar"**.
   * El estado cambiará a **"Conectado"** (verde) en ambos dispositivos.
3. **¡A compartir!**
   * **De PC a Celular:** Copia cualquier texto en tu PC (`Ctrl + C`). Inmediatamente estará en el portapapeles de tu celular listo para pegar (`Pegar`).
   * **De Celular a PC:** Selecciona cualquier texto en cualquier aplicación del celular, pulsa **Compartir**, elige **"Enviar a PC (TowerShare)"**, y de inmediato lo tendrás en el portapapeles de Windows listo para `Ctrl + V`.

---

## 📦 Subir a GitHub

Para inicializar o subir este proyecto a tu repositorio de GitHub, ejecuta desde la raíz de este directorio:

```bash
git add .
git commit -m "Initial commit: TowerShare Windows client and Android server"
git branch -M main
git remote add origin https://github.com/TU_USUARIO/TU_REPOSITORIO.git
git push -u origin main
```

---

## 🔒 Privacidad y Seguridad
* La transferencia se realiza exclusivamente punto a punto vía Bluetooth RFCOMM con el cifrado de enlace propio de la norma Bluetooth.
* No se guardan historiales de texto en bases de datos ni se recopila telemetría.
