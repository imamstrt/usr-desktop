# AI_CONTEXT.md — Architectural & Technical Context for SMI Wi-Fi Provisioning Tools (.NET 9 WPF)

Dokumen ini disusun sebagai panduan konteks teknis dan arsitektural yang komprehensif bagi pengembang (*human developer*) maupun *Artificial Intelligence* (AI) untuk memahami, mengoperasikan, memodifikasi, dan memelihara proyek peranti lunak **SmiWifiProvisioning**.

---

## 1. Identitas & Stack Teknologi

* **Nama Solusi:** `SmiWifiProvisioning`
* **Target Framework:** `.NET 9.0 (Windows)` (`net9.0-windows`)
* **Tipe Aplikasi:** Windows Desktop WPF (Windows Presentation Foundation)
* **Resolusi Window Utama:** Default `1100x700` (Minimum `900x550`, `ResizeMode="CanResizeWithGrip"`)
* **Pola Arsitektur:** **MVVM (Model-View-ViewModel)** dengan *View-Model First Navigation*
* **Dependencies Utama (NuGet Packages):**
  * **`CommunityToolkit.Mvvm` (v8.4.2):** Menyediakan *Source Generators* (`[ObservableProperty]`, `[RelayCommand]`), `ObservableObject`, dan infrastruktur binding MVVM.
  * **`System.IO.Ports` (v10.0.11):** Mengelola komunikasi serial hardware UART / RS485 / Virtual COM Port.
* **Pengaturan Publish Single File & Kompresi (`.csproj`):**
  * `EnableCompressionInSingleFile = true` (Mengompresi output *publish* hingga ~35 MB untuk mode *Self-Contained*).

---

## 2. Struktur Pohon Berkas Proyek (Root Directory)

```text
SmiWifiProvisioning/
├── SmiWifiProvisioning.csproj    # File project, NuGet dependencies & Publish Single-File settings
├── AI_CONTEXT.md                # Berkas panduan arsitektur, konteks AI/Developer & spesifikasi AT
├── App.xaml                    # Application Entry Point XAML
├── App.xaml.cs                  # Application Startup Logic
├── Models/
│   ├── ProductionConfig.cs       # Konstanta terpusat (IP Server, Port, Baudrate, Timeout, AtRetryLimit)
│   └── WifiNetwork.cs            # Model data jaringan Wi-Fi hasil AT+WSCAN (RSSI, Bar visual, Security)
├── Services/
│   ├── Interfaces/
│   │   ├── ICsvLoggerService.cs  # Interface layanan pencatatan log CSV produksi
│   │   └── IUsrDeviceService.cs  # Interface mesin komunikasi serial hardware USR
│   └── Implementations/
│       ├── CsvLoggerService.cs   # Implementasi thread-safe (SemaphoreSlim) penulisan log CSV
│       └── UsrDeviceService.cs   # Implementasi logika serial UART & sekuens AT Command
├── ViewModels/
│   ├── MainViewModel.cs           # Shell ViewModel pembawa navigasi sidebar & CurrentViewModel
│   ├── ConfigProduksiViewModel.cs # ViewModel lini produksi (Barcode Scanner, <10 char warning, Auto-Provisioning)
│   ├── ConfigUsrViewModel.cs      # ViewModel pengujian Wi-Fi User (Scan, Connect, DHCP)
│   └── ConfigAdminViewModel.cs    # ViewModel audit & kontrol parameter AT (Socket A, Socket B, MQTT, Master Batch Read)
└── Views/
    ├── MainWindow.xaml            # Window utama berisi Sidebar Menu (Width 200) & ContentControl
    ├── MainWindow.xaml.cs         # Code-behind MainWindow
    ├── ConfigProduksiView.xaml    # UserControl UI Lini Produksi
    ├── ConfigProduksiView.xaml.cs # Code-behind ConfigProduksiView (Auto-focus & Auto-scroll log)
    ├── ConfigUsrView.xaml         # UserControl UI Pengujian Wi-Fi User
    ├── ConfigUsrView.xaml.cs      # Code-behind ConfigUsrView
    ├── ConfigAdminView.xaml       # UserControl UI Panel Admin (Zebra striping, Socket A & B, MQTT panel)
    ├── ConfigAdminView.xaml.cs    # Code-behind ConfigAdminView (Auto-scroll terminal log)
    ├── WifiScanResultWindow.xaml  # Window Pop-up Modal berisi DataGrid hasil Scan Wi-Fi
    └── WifiScanResultWindow.xaml.cs # Code-behind WifiScanResultWindow (Double-click item handler)

3. Spesifikasi Komunikasi Hardware (Modul USR Serial-to-Wi-Fi)Modul USR-DR404 / USR Series berkomunikasi melalui jalur serial (UART/RS485)
.A. Alur Jabat Tangan Mode AT (Handshake Protocol)Secara default, modul beroperasi pada Transparent Mode (mengirim/menerima data mentah).
Untuk konfigurasi AT Command, aplikasi mengeksekusi sekuens berikut:Guard Time Initial: Jeda $\ge 400\text{ ms}$ tanpa trafik data.Kirim Guard Code: Mengirim string +++ tanpa karakter \r\n.Wait Response 'a': Modul membalas karakter 'a' (timeout $\sim 600\text{ ms}$).Confirm Character 'a': Aplikasi mengirim balik karakter 'a' tanpa \r\n.

Verify Mode AT: Modul membalas +ok\r\n\r\n. Perangkat resmi masuk Mode AT.
Exit Mode AT: Mengirim AT+ENTM\r\n. Modul membalas +ok dan kembali ke Transparent Mode.
Wajib memberikan jeda Task.Delay(300) sebelum port.Close() agar buffer OS sempat terkirim fisik.
B. Otomatisasi Scanning Baudrate (ScanBaudRateAsync)Aplikasi menguji daftar baudrate standar secara otomatis (300, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200)
:Skenario Normal: Mengirim +++ (Modul di Transparent Mode).Skenario Recovery: Mengirim AT\r\n (Modul tertahan di Mode AT).
C. Pemrosesan Serentak Modul Admin (Batch Execution)Menggunakan SendBatchAtCommandsAsync untuk membaca seluruh parameter pada halaman Admin dalam 1 sesi 
Mode AT:$$\text{Masuk AT Mode} \rightarrow \text{Kirim } N \text{ Perintah AT berturut-turut} \rightarrow \text{Keluar AT Mode}
$$Siklus scan baudrate dan handshake +++ hanya dilakukan 1 kali untuk seluruh perintah.
D. Penanganan Anomali Buffer HEX & Padding Modul USRPerilaku Hardware:
 Modul USR mengalokasikan fixed-size buffer internal ($\sim 6\text{ Byte}$) pada register AT+REGUSR.String Ganjil: 
 Jika input Meter ID berpanjang ganjil (misal: "3333333"), aplikasi menggenapkannya dengan '0' di akhir ("33333330").
 Interpretasi HEX: String 33333330 pada mode HEX diterjemahkan modul sebagai 4 Byte data binary (0x33 0x33 0x33 0x30).
 Karena kurang dari batas minimal buffer hardware, modul secara otomatis menyisipkan padding internal (3320 atau 33)
 sehingga saat kueri AT+REGUSR dibaca kembali, hasilnya dapat berupa 333333303320 atau 3333333033.
 Proteksi UI/UX: Diimplementasikan peringatan MessageBox pada ConfigProduksiViewModel dan ConfigAdminViewModel 
 jika input ID/Payload kurang dari 10 karakter untuk memberi tahu operator.
 
 Command AT,Jenis,Parameter / Format,Fungsi / Penjelasan
AT+VER,Query,(Tanpa param),Membaca versi firmware modul.
AT+WMODE,Set / Query,APSTA / AP / STA,"Mengatur mode Wi-Fi (Dual AP+STA, AP Only, STA Only)."
AT+WAP,Set / Query,"11BGN,{SSID},{Channel}",Mengatur parameter Access Point bawaan modul.
AT+WAKEY,Set / Query,"WPA2PSK,AES,{Password}",Mengatur enkripsi & password AP modul.
AT+NETP,Set / Query,"{Proto},{CS},{Port},{IP}","Mengatur Socket A utama (TCP/UDP/HTTP/MQTT, CLIENT/SERVER, Port, IP)."
AT+WEBU,Set / Query,"{Username},{Password}",Mengatur kredensial login web admin internal modul.
AT+EVENT,Set / Query,off / on,Mematikan/mengaktifkan notifikasi event internal.
AT+REGEN,Set / Query,USR / OFF / SN / MAC / CLOUD,Mengatur mode paket registrasi socket TCP diawal koneksi.
AT+REGUSR,Set / Query,"{HEX/ASC},{Data}",Mengisi isi data paket registrasi (di-padding jika < 10 char).
AT+REGSND,Set / Query,FIRST / EVERY,Pengiriman registrasi (1x diawal koneksi atau setiap paket).
AT+TCPTO,Set / Query,{detik},Mengatur durasi timeout TCP Socket A (≤600s).
AT+NETHEARTCFG,Set / Query,"{ON/OFF},{detik},{HEX/ASC},{Payload}",Konfigurasi Heartbeat TCP Socket A (default interval: 900s).
AT+SOCKB,Set / Query,"{Proto},{Port},{IP}",Mengatur Socket B Sekunder (TCP/UDP/UDPS/IGMP/OFF).
AT+TCPDISB,Set / Query,"{on/off}[,{0/1}]","Mengatur/memutus koneksi TCP Client Socket B (0: RAM, 1: Flash)."
AT+TCPTOB,Set / Query,{detik},Mengatur durasi timeout TCP Socket B.
AT+TCPLKB,Query,(Tanpa param),Membaca status koneksi aktif Socket B (on/off).
AT+UDPLCPT,Set / Query,"{PortA},{PortB}",Mengatur port UDP lokal untuk Socket A dan Socket B.
AT+WSSSID,Set / Query,{SSID},Mengatur SSID Wi-Fi target yang dihubungi (Station Mode).
AT+LAN,Set / Query,{IP},Mengatur IP Address LAN lokal modul.
AT+WANN,Query,(Tanpa param),Membaca status IP WAN / DHCP yang diterima dari router.
AT+SN,Query,(Tanpa param),Membaca Serial Number fisik modul USR.
AT+WSMAC,Query,(Tanpa param),Membaca MAC Address Station Wi-Fi modul.
AT+WIFI,Set / Query,on / off,Saklar pengaktif/nonaktif radio Wi-Fi.
AT+WSLK,Query,(Tanpa param),Membaca status link koneksi Wi-Fi Station (Disconnected/Connected).
AT+RELD,Set / Exec,(Password Protected: 123smi321),Pengembalian ke pengaturan PABRIK (Factory Reset).
AT+UART,Set,"{Baud},{Data},{Stop},{Parity},{FC}","Mengunci serial UART (default: 19200,8,1,None,NFC)."
AT+WSCAN,Execution,(Tanpa param),Memindai Access Point Wi-Fi sekitar (Timeout 15s).
AT+Z,Execution,(Tanpa param),Soft Reboot perangkat untuk menerapkan seluruh perubahan.
AT+ENTM,Execution,(Tanpa param),Keluar dari Mode AT kembali ke Transparent Mode.




5. Rincian Komponen Class & Layer Architecture
A. Layer Models (Models/)
ProductionConfig.cs: Menyimpan konstanta pengaturan lini produksi 
(ServerIp = "smarttoken.id", ServerPort = 5001, ApPassword/WebPassword = "SMI@123321", TargetBaudRate = 19200, AtRetryLimit = 2, AtDelayMs = 600, LogDir = "logs").

WifiNetwork.cs: Representasi Wi-Fi hasil pemindaian AT+WSCAN (Ssid, Security, Rssi, IsOpen, serta properti SignalBar pengubah RSSI ke indikator visual ███).

B. Layer Service (Services/)
IUsrDeviceService.cs & UsrDeviceService.cs:

ProvisionDeviceAsync(): Eksekusi sekuens otomatis 13 perintah AT untuk lini produksi.

ScanWifiAsync(): Eksekusi AT+WSCAN, parsing CSV, filter jaringan Open, pengurutan sinyal terkuat.

ConnectWifiAsync(): Mengirim kredensial AT+WSSSID & AT+WSKEY, lalu reboot AT+Z.

CheckDhcpAsync(): Membaca status alokasi IP WAN modul (AT+WANN).

SendSingleAtCommandAsync(): Eksekusi 1 perintah AT tunggal untuk pengujian individual di Admin.

SendBatchAtCommandsAsync(): Optimasi performa master read serentak dalam 1 sesi Mode AT.

ICsvLoggerService.cs & CsvLoggerService.cs:

WriteLogAsync(): Mencatat riwayat produksi ke logs/production_log_YYYYMMDD.csv secara thread-safe menggunakan SemaphoreSlim(1,1).

C. Layer ViewModels (ViewModels/)
MainViewModel.cs: Controller navigasi Sidebar Menu yang mengelola CurrentViewModel.

ConfigProduksiViewModel.cs: Logika bisnis Lini Produksi. Menangani pemicu Barcode Scanner (Key Enter), penguncian UI (IsBusy), 
pemanggilan logger CSV, dan pop-up MessageBox konfirmasi jika MeterId.Length < 10.

ConfigUsrViewModel.cs: Logika bisnis Pengujian Wi-Fi User. Membuka modal WifiScanResultWindow dan melakukan koneksi target AP.

ConfigAdminViewModel.cs: Logika bisnis Audit & Parameter Admin.

Visual Zebra Striping & Sub-Label: Menampilkan judul ramah pengguna beserta teks AT Command asli (misal: mode usr [AT+WMODE]).

Pengelolaan Socket A & Socket B: Mendukung pembacaan & pengiriman parameter Socket A (AT+NETP) dan Socket B (AT+SOCKB, AT+TCPDISB, AT+TCPTOB, AT+TCPLKB, AT+UDPLCPT).

MQTT Extra Panel: Menampilkan panel dinamis (Client ID, User, Password, Sub Topic, Pub Topic) saat protokol MQTT dipilih pada AT+NETP.

RELD Password Protection: Mewajibkan kata sandi "123smi321" sebelum mengeksekusi AT+RELD (Factory Reset).

Master Batch Read (ReadAllConfigsCommand): Membaca 23 parameter sekaligus dalam waktu 1-2 detik.

D. Layer Views (Views/)
MainWindow.xaml: Shell UI utama penyedia Sidebar (lebar 200px) dan ContentControl kanan.

ConfigProduksiView.xaml: Layout produksi dengan terminal log hitam auto-scroll.

ConfigUsrView.xaml: Layout pengujian koneksi Wi-Fi.

ConfigAdminView.xaml: Layout form berpola Zebra Striping (warna latar #FFFFFF dan #F9FAFB selang-seling), MinWidth="980", serta dukungan scroll vertikal dan horizontal.

WifiScanResultWindow.xaml: Modal Pop-up DataGrid hasil pemindaian Wi-Fi.

6. Peta Arsitektur & Peta Dependensi (Class Diagram Flow)

[MainWindow.xaml]
      │ (DataContext)
      ▼
[MainViewModel] ──(Controls Navigation)──► [ConfigProduksiViewModel] ──► [IUsrDeviceService] ──► [UsrDeviceService]
      │                                    [ConfigUsrViewModel]       ──► [ICsvLoggerService]  ──► [CsvLoggerService]
      │                                    [ConfigAdminViewModel]     ──► [ProductionConfig]
      ▼
[Views mapping via DataTemplate]
  ├── ConfigProduksiView.xaml  <──> ConfigProduksiViewModel
  ├── ConfigUsrView.xaml        <──> ConfigUsrViewModel  <──► [WifiScanResultWindow.xaml] (Popup)
  └── ConfigAdminView.xaml     <──> ConfigAdminViewModel
  
  7. Panduan Pemeliharaan & Roadmap Fitur Baru (Future Development)
A. Mengubah Kredensial Pabrik / Server Target
Cukup perbarui berkas Models/ProductionConfig.cs. Seluruh Service dan ViewModel akan merujuk ke nilai baru tersebut secara otomatis.

B. Menambahkan Parameter AT Baru di Halaman Admin
Tambahkan deklarasi private field dengan atribut [ObservableProperty] di ViewModels/ConfigAdminViewModel.cs.

Buat [RelayCommand] untuk fungsi Read dan Send parameter tersebut.

Tambahkan baris <Border> / <Grid> baru pada Views/ConfigAdminView.xaml.

Masukkan nama perintah AT baru tersebut ke dalam cmdList pada fungsi ReadAllConfigsAsync di ConfigAdminViewModel.cs agar ikut terbaca saat tombol 🔍 Baca Semua Parameter ditekan.

Lakukan Rebuild Solution (Ctrl + Shift + B) agar Source Generators me-generate properti publiknya.

C. Roadmap Pengembangan Wireless UDP (IUsrUdpService)
Guna mendukung konfigurasi nirkabel tanpa kabel serial COM Port:

Aturan Protocol UDP Broadcast: Modul USR mendengarkan paket discovery UDP pada Port 48899.

Desain Interface Baru (IUsrUdpService.cs):

SearchDevicesUdpAsync(): Mengirim paket broadcast UDP (0xFF, 0xFF, 0xFF... atau string kata kunci USR) untuk mendeteksi seluruh modul aktif di jaringan Wi-Fi lokal.

SendAtCommandUdpAsync(IPAddress deviceIp, string atCmd): Mengirimkan perintah AT langsung via socket UdpClient ke port 48899 tanpa perlu sekuens handshake +++.

Pemisahan Layer Service: Buat berkas implementasi Services/Implementations/UsrUdpService.cs 
terpisah dari UsrDeviceService.cs (Serial) agar arsitektur kode tetap bersih (Separation of Concerns).