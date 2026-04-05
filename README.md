# neTiPx

<p align="center">
  🌍 Language: [English](README.md) | [Deutsch](README.de.md)
</p>

<p align="center">
  <img src="Bilder/toolicon.png" alt="neTiPx Logo" width="120"/>
</p>

**neTiPx** is a modern desktop tool for Windows for comfortable management of network adapters and IP configurations. With an intuitive user interface, neTiPx provides quick access to all important network settings and information.

---

## 📋 Table of Contents

- [Features](#-features)
- [Screenshots](#-screenshots)
  - [Adapter Overview](#adapter-overview)
  - [IP Configuration](#ip-configuration)
  - [Ping Tool](#ping-tool)
  - [WLAN Scanner](#wlan-scanner)
  - [Network Calculator](#network-calculator)
  - [Network Scanner](#network-scanner)
  - [Log Viewer](#log-viewer)
  - [Routes Tool](#routes-tool)
  - [Info](#info)
  - [Settings](#settings)
- [Features in Detail](#-features-in-detail)
  - [PING Tool](#ping-tool-1)
  - [Ping Logging](#ping-logging)
  - [Log Viewer](#log-viewer-1)
  - [WLAN Scanner - Technical Details](#wlan-scanner---technical-details)
  - [Network Scanner - Technical Details](#network-scanner---technical-details)
  - [Route Management and Routing Analysis](#route-management-and-routing-analysis)
- [System Requirements](#-system-requirements)
- [Installation](#-installation)

---

## ✨ Features

- 🔌 **Adapter Management**: Overview of up to two network adapters with detailed information
- 🌐 **IP Profile Manager**: Manage multiple IP profiles for quick switching between network configurations
- 📊 **Network Information**: Detailed display of IPv4/IPv6 addresses, gateway, DNS and MAC addresses
- 🎯 **Connection Status**: Real-time ping monitoring of gateway and DNS servers with visual traffic light indicator
- 🎨 **Theme Support**: Customizable color themes (Light/Dark/System) with multiple predefined color schemes
- 📍 **System Tray Integration**: Minimize to taskbar with hover window for quick network info
- 🚀 **Autostart**: Optionally start with the system
- 🛰️ **PING Tool**: Monitor multiple targets in parallel (IPv4/IPv6), enable/disable per target
- 📝 **Ping Logging**: Automatic log files per target including opening, exporting and deleting
- 🧭 **Background Operation**: Pings continue optionally when the ping page is not active
- 📡 **WLAN Scanner**: Native Windows API for detailed WLAN network information
- 🧮 **Network Calculator**: IP subnet calculations with intelligent range detection and bidirectional synchronization
- 🔎 **Network Scanner**: Scan IP ranges with port checking and detailed view of found devices
- 📄 **Log Viewer**: Open and live display of log files with filtering, highlight rules, 16-color swatch selection and optional auto-scroll
- 🛣️ **Routes Tool**: Display current IPv4 routes including delete function for user-defined/persistent routes and direct addition of new routes
- 🧩 **Modular Tools Page**: Ping, WLAN, Network Calculator, Network Scanner, Log Viewer and Routes as separate subpages with lazy loading
- 🗂️ **Page Visibility**: Main and tool pages can be shown/hidden via `PagesVisibility.xml`
- 🛠️ **Hidden Admin Configuration**: On the Settings page, the word `Wünschen` opens a dialog for managing page visibility

Back to
[Table of Contents](#-table-of-contents)
---

## 📸 Screenshots

### Adapter Overview

The Adapter page displays detailed information about your configured network adapters:

![Adapter Overview](Bilder/Adapter_Page.png)

**Displayed Information:**
- Name and MAC address of the adapter
- IPv4 addresses with subnet masks
- IPv6 addresses
- Gateway addresses (IPv4 and IPv6)
- DNS servers (IPv4 and IPv6)
- Clear display for up to two adapters simultaneously

### IP Configuration

Manage multiple IP profiles and quickly switch between different network configurations:

![IP Configuration](Bilder/IP_Konfigurations_Page.png)

**Features:**
- **Profile Manager**: Create, edit and delete IP profiles
- **DHCP or Manual**: Choose between automatic and manual IP configuration
- **Multiple IP Addresses**: Assign multiple IP addresses to an adapter
- **DNS Configuration**: Configure primary and secondary DNS servers
- **Routes per Profile**: Manage static IPv4 routes directly in the IP profile
- **Route Mode**: Choose between `replace` and `add` existing persistent routes per profile
- **System Reconciliation**: Existing system routes are detected and marked in the profile dialog
- **Real-time Connection Status**: Monitor gateway and DNS servers with color-coded traffic light
  - 🟢 Green: Reachable (good ping)
  - 🟡 Yellow: Reachable (slow ping)
  - 🔴 Red: Unreachable
- **Ping Display**: Shows current ping times for gateway and DNS servers

### Ping Tool

The Ping Tool enables monitoring of multiple targets with individual timing and protocol display:

![Ping Tool](Bilder/tools_ping.png)

**Features:**
- **Multiple Targets**: Add and monitor IPs or hostnames in parallel
- **Interval per Target**: Individual ping frequency per entry
- **IPv4/IPv6 Display**: Response time and status indicator per protocol
- **Active Status per Line**: Enable and disable individual targets independently
- **Background Option**: Pings continue optionally even when the Ping page is not in focus
- **Status for Unused Protocols**: Display `inactive` with gray indicator

### WLAN Scanner

The WLAN Scanner uses the native Windows WLAN API for detailed network information:

![WLAN Scanner](Bilder/tools_wlan.png)

**Features:**
- **Native API**: Direct access to Windows WLAN interface
- **Sortable Table**: Click column headers to sort
  - 📶 Signal Symbol (strength visualization)
  - SSID (Network name)
  - Signal (Percent)
  - BSSID (MAC address of access point)
- **Detailed Information** in three areas:
  - **Signal**: Strength (%), Quality (%), RSSI (dBm)
  - **Frequency**: Band (2.4G/5G/6G), Channel, Frequency (MHz)
  - **Security & Standard**: Encryption (🔓 secured / 🔒 open), PHY Type (802.11a/b/g/n/ac/ax), Network Type
- **Band Detection**: Automatic detection of 2.4 GHz, 5 GHz and 6 GHz (Wi-Fi 6E)
- **Signal Symbols**:
  - 📶 Strong (≥75%)
  - 📳 Medium (50-74%)
  - 📴 Weak (25-49%)
  - ❌ Very Weak (<25%)

### Network Calculator

The Network Calculator provides intelligent IP subnet calculations with automatic synchronization:

![Network Calculator](Bilder/tools_NetCalc.png)

**Features:**
- **Intelligent Input**: IP address, subnet mask or CIDR suffix - all fields update automatically
- **Bidirectional Synchronization**:
  - Change subnet mask → automatic calculation of CIDR suffix and max hosts
  - Change CIDR suffix → automatic calculation of subnet mask and max hosts
  - Change max hosts → automatic calculation of subnet mask and CIDR suffix
- **Plus/Minus Control**: Quick switching between valid host counts (e.g. 254 → 510 → 1022)
- **Automatic Calculation**: Results displayed immediately with valid input
- **IP Range Detection**: Automatic classification of entered IP:
  - Private Range (10.x.x.x, 172.16-31.x.x, 192.168.x.x)
  - Public Range
  - Loopback (127.x.x.x)
  - Zeroconf/Link-Local (169.254.x.x)
  - Multicast (224.x.x.x - 239.x.x.x)
  - Shared Address Space/CGNAT (100.64.x.x)
  - Documentation Range
  - Broadcast, Unspecified, Reserved
- **Detailed Results**:
  - Network address and broadcast address
  - First and last usable IP
  - Subnet mask and CIDR suffix
  - Number of available hosts
  - Wildcard mask

### Network Scanner

The Network Scanner searches local IP ranges and displays detected devices including port status.

![Network Scanner](Bilder/tool_NetScan.png)

**Features:**
- **IP Range Scanning**: Single ranges or multiple ranges in one request
- **Port Checking**: Freely configurable port list for reachability and service checking
- **Device List with Details**: Overview of detected hosts with detail area for quick evaluation
- **Direct Actions**: Open ports can be opened with the default application by double-click

### Log Viewer

The Log Viewer opens existing log files and displays new entries live in a separate tool subpage.

![Log Viewer](Bilder/tools_LogViewer.png)

**Features:**
- **File Selection and History**: Recently used log files can be reopened directly
- **Live Display**: New entries are automatically appended to the existing view
- **Filter and Search**: Free-text filter with hit counter (`visible / total`) and instant update
- **Highlight Rules**: Search terms can be color-highlighted
- **Color Selection via Swatches**: Selection via colored rectangle swatches instead of text list
- **Extended Color Palette**: 16 selectable colors for highlights
- **Automatic Scrolling**: Optional auto-scroll to end of file as new entries arrive
- **Robust Reloading**: Display remains readable even while file is being written by another process

### Routes Tool

The Routes Tool displays the current IPv4 routing table and supports targeted analysis for a specific destination.

![Routes Tool](Bilder/tools_routen.png)

### Info

The Info page consolidates version and update information along with important links.

![Info](Bilder/Infos.png)

**Features:**
- **Route Overview**: Display of active and persistent IPv4 routes including default route (`0.0.0.0/0`)
- **Delete Logic by Source**: Delete button only for user-defined/static routes, system routes marked as `System Route`
- **Target IP Filter**: Enter target IP to display only relevant routes (Longest Prefix Match + Metric)
- **Sortable Table**: Sort via column headers with direction indicator (`▲`/`▼`)
- **Add Route**: Create persistent route directly from the tool

### Settings

Configure the application to your needs:

![Settings](Bilder/Einstellungen_Page.png)

**Configuration Options:**

#### 📡 Network Adapters
- **Adapter 1 & 2**: Select the two main adapters displayed on the Adapter page
- Only active network adapters are available for selection

#### 🔔 System Tray
- **Hover Window**: Display network information when hovering over the tray icon
- **Minimization**: Option to minimize to taskbar instead of closing

#### 🚀 Autostart
- **Windows Startup**: Automatically start the application at system startup
- **Minimized Start**: Start the application minimized in system tray

#### 📝 Ping Logging
- **Choose Log Folder**: Custom storage location for ping logs
- **Default Folder**: Quick reset to default path
- **Path Display**: Dynamically adjusted single-line display with tooltip for full path

#### 🗂️ Page Visibility
- **Configuration File**: `%APPDATA%\neTiPx\PagesVisibility.xml`
- **Hidden Dialog**: Click the word `Wünschen` in Settings
- **Grouped Control**: Separate areas for `Main Pages` and `Tools`
- **Tools Dependency**:
  - `Tools (Main Page)` off => all tool sub-pages off
  - One tool sub-page on => `Tools (Main Page)` automatically on
  - All tool sub-pages off => `Tools (Main Page)` automatically off
- **Always Visible**: `Adapter Info`, `Info` and `Settings` are permanently visible and cannot be hidden via XML
- **Live Update**: When closing the dialog, XML values are saved and navigation is updated immediately

#### 🎨 Color Themes
- **Theme Selection**: Choose from multiple predefined color themes
  - Light/Dark/System
  - Red, Blue, Green, Orange, Purple, Teal
- **Custom Themes**: Create and edit your own color themes
- **Theme Editor**: Customize background, text and accent colors individually

#### 🌐 Language Selection

- The application supports multiple languages. Select the display language from the dropdown menu in Settings.
- The dropdown displays the native names of languages (e.g. "Deutsch", "English", "Español"). These are dynamically loaded from language files.
- Language changes take effect immediately on the entire user interface.

Back to
[Table of Contents](#-table-of-contents)
---

## 🔧 Features in Detail

### PING Tool

- **Parallel Monitoring**: Multiple targets are monitored simultaneously
- **Target Types**: Supports IPv4, IPv6 and hostnames
- **Visible Protocol Behavior**:
  - Unused protocol displays `inactive` and a gray indicator
  - Disabled target displays `Disabled` for both protocols
- **Flexible Activation**:
  - Per target via row checkbox
  - Global for background operation via `continue active in background`

### Ping Logging

- **Individual Log File per Target**: Unique filenames, even with special characters in target name
- **CSV Format with Timestamp**: `Time;Target;Protocol;Response Time`
- **Direct Actions in List**:
  - Open log file
  - Optionally delete with
  - Before deleting, optionally export via `Save As`
- **Protocol-Specific Logging**: Only relevant IPv4/IPv6 entries are written

### Log Viewer

- **Supported Formats**: Opens log, text, CSV and JSON files for quick visual inspection
- **Live Append Instead of Full Reload**: New data is appended to existing display without rebuilding the entire file each time
- **Highlight Rules with Color Swatches**: Rules can be created, removed, imported and exported; color selection via visual swatches
- **16 Highlight Colors**: Extended palette for better visual distinction in logs
- **Filter with Hit Counter**: Text search with highlighting and display `visible / total`
- **Auto-Scroll Optional**: With option enabled, view stays at end of file; when disabled, current position is retained
- **Complete Localization**: All visible Log Viewer texts including highlight dialog come from language files
- **Fault-Tolerant Reading**: File is opened with shared access so actively written logs can be observed

### WLAN Scanner - Technical Details

- **Native Windows WLAN API**: Direct P/Invoke access to wlanapi.dll
  - WlanOpenHandle: WLAN interface initialization
  - WlanEnumInterfaces: List available WLAN adapters
  - WlanGetNetworkBssList: Retrieve detailed BSS information
- **Thread-Safe UI Updates**: DispatcherQueue for safe updates from background threads
- **Comprehensive Network Information**:
  - Signal: dBm, Percent, Link Quality
  - Frequency: MHz, Channel, Band (2.4/5/6 GHz)
  - Security: Privacy Bit, Encryption Status
  - Standard: PHY Type (802.11 variants), Network Type (Infrastructure/Ad-Hoc)
  - Hardware: BSSID, Beacon Interval
- **Robustness**: Automatic fallback to netsh command line if API issues occur

### Network Scanner - Technical Details

- **Asynchronous Scanning**: Non-blocking host and port checks for smooth operation
- **Cancellable**: Running scans can be controlled stop
- **Sortable Results List**: Devices can be ordered by relevant columns
- **Detail View per Device**: Summarized host information and detected open ports

### Route Management and Routing Analysis

- **Source-Based Classification**: Combination of `route print`, CIM (`Win32_IP4PersistedRouteTable`) and `Get-NetRoute` to distinguish system and user routes
- **Persistence Detection**: Static/persistent routes are detected as deletable, system-level on-link/local/DHCP routes remain protected
- **Routing Decision in Filter**: For target IPs, only candidates with best prefix and best metric are displayed
- **Safe Delete/Add Operations**: Route changes are performed elevated and table is re-read after action

### IP Profile Management

- **Multiple Profiles**: Save different network configurations for various locations (Office, Home Office, External)
- **Quick Switching**: Switch between saved profiles with just a few clicks
- **DHCP Support**: Automatic IP configuration via DHCP
- **Manual Configuration**: Detailed control over IP addresses, subnet masks, gateway and DNS
- **Integrated Route Management**: Profile-specific static IPv4 routes with dialog for management and system reconciliation
- **Validation**: Automatic verification of entered IP addresses and network configuration
- **Multi-IP**: Assign multiple IP addresses to an adapter simultaneously

### Connection Quality

- **Automatic Monitoring**: Continuous pinging of gateway and DNS servers (every 5 seconds)
- **Visual Display**: Color-coded traffic light shows status at a glance
- **Ping Times**: Detailed display of response times in milliseconds
- **Multiple Monitoring**: Simultaneous monitoring of gateway, DNS1 and DNS2

### Theme System

- **Customizable Interface**: Adapt the appearance of the application to your preferences
- **Predefined Themes**: Multiple professional color schemes to choose from
- **Real-Time Preview**: See changes immediately in the application

Back to
[Table of Contents](#-table-of-contents)
---

## 💻 System Requirements

- **Operating System**: Windows 10 Version 1809 (Build 17763) or later
- **Framework**: .NET 8.0 Runtime
- **UI Framework**: WinUI 3 (Windows App SDK) - **required**
- **Permissions**: Administrator rights for changes to network settings

### Windows App SDK

neTiPx requires **Windows App SDK 1.8.5** to run. If you receive the following error:

![Missing Windows App SDK](Bilder/FehlendeMSIX.png)

Download and install the Windows App SDK from:
[Microsoft Windows App SDK Downloads](https://docs.microsoft.com/windows/apps/windows-app-sdk/downloads)

---

## 📦 Installation

### Installation

1. **Check System Requirements**: Ensure that Windows App SDK is installed (see [System Requirements](#-system-requirements))
2. Download the latest setup package from the [Releases](../../releases) section
3. Run `neTiPx_Setup_Vx.x.x.x.exe`
4. Follow the installation wizard instructions
5. Start neTiPx from the Start menu or desktop icon

**Notes**:
- Administrator rights are required for changes to network settings.
- If you receive an error message about Windows App SDK at startup, see [System Requirements](#windows-app-sdk).

Back to
[Table of Contents](#-table-of-contents)
---

## 📄 License & Contact

See `LICENSE` in the repository. For code questions, please use Issues/PRs in the repo.

https://buymeacoffee.com/pedrotepe

Back to
[Table of Contents](#-table-of-contents)
