# Beanfun 經典 UI — 舊版介面維護分支

[![GitHub all releases](https://img.shields.io/github/downloads/BoringMan314/bm-beanfun-classic/total)](https://github.com/BoringMan314/bm-beanfun-classic/releases)

>  **遊戲橘子數位科技旗下遊戲的第三方啟動器**

⚠️ **免責聲明：** 本程式 **不是** 遊戲橘子數位科技開發的官方客戶端程式。關於遊戲帳號使用第三方的方式登入，請再三斟酌，並請確認您下載當前程式的途徑是否安全。

* 基於 Beanfun [v5.9.1.2604180731](https://github.com/pungin/Beanfun/releases/tag/v5.9.1.2604180731)
* Beanfun 經典 UI — 舊版介面維護分支
* 程式使用 [Locale_Remulator](https://github.com/InWILL/Locale_Remulator) 作為區域模擬元件，支持 32-bit 和 64-bit 遊戲。

---

![程式畫面示意](screenshot/screenshot.png)

---

## 下載與使用 (Getting Started)

### 系統要求 (Prerequisites)
* **作業系統：** Windows 10 或以上
* **必要元件：** [Microsoft Visual C++ Redistributable](https://docs.microsoft.com/zh-CN/cpp/windows/latest-supported-vc-redist?view=msvc-170)

### 使用方法 (Usage)
1. 前往 **[最新發行版 (Releases)](https://github.com/BoringMan314/bm-beanfun-classic/releases/latest)** 下載最新的 `BeanfunClassic.exe`。
2. 下載後放在任意全英文路徑的資料夾，直接運行即可。

> 💡 **運作原理說明：** 啟動遊戲時，程式會在當前資料夾釋放 `LRProc.dll` 和 `LRHookx32.dll` 或 `LRHookx64.dll` 文件。
> * `LRProc.dll` - 將 Hook dll 載入到遊戲中
> * `LRHookx32.dll` 或 `LRHookx64.dll` - 區域模擬元件

---

## 技術棧 (Built With)

* **[.NET 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)** - 目標框架（Self-contained，使用者不需另外安裝 Runtime）
* **[ini-parser-netstandard](https://github.com/lukazh/ini-parser-standard)** - ini 設定檔元件
* **[log4net](https://logging.apache.org/log4net/)** - 日誌記錄元件
* **[Newtonsoft.Json](https://www.newtonsoft.com/json)** - JSON 解析元件
* **[Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2)** - 內嵌瀏覽器元件
* **[Detours](https://github.com/microsoft/Detours)** - 用於 Hook ANSI/Unicode 函數
* **[Locale_Remulator](https://github.com/InWILL/Locale_Remulator)** - 區域模擬元件

---

## 維護與貢獻規範 (Maintenance & Contribution)

為了確保經典版維護品質，所有貢獻者請遵循以下標準開發流程：

### 流程：Branch ➔ Test ➔ PR ➔ Format ➔ Approve

1.  **建立分支**：從本倉庫建立功能或修復分支進行開發。
2.  **本地測試 (Local Test)**：
    * 在完成功能開發或 Bug 修復後，務必在本地環境進行編譯與功能測試。
    * 確保程式能正常啟動，且不會影響現有的登入功能。
3.  **提交 Pull Request (PR)**：
    * 將你的改動 Push 到功能分支，並向本專案發起 PR。在 PR 描述中清晰說明你修改的內容與原因。
4.  **執行程式碼格式化 (CSharpier) [強制]**：
    * 提交前請務必執行 CSharpier 確保代碼風格一致，否則 CI 檢查可能會失敗。
    ```bash
    dotnet tool restore
    dotnet csharpier format .
    ```
5.  **審核與合併 (Approve & Merge)**：
    * 維護者審核通過並合併 PR 後，視需要手動更新版號並觸發 Release workflow。

---

## 開發與發佈 (Development & Release)

### 發佈流程 (CI/CD)

本專案版號採手動指定，目前固定為 `5.9.2.1`。如需改版，請先更新 `Beanfun/Properties/AssemblyInfo.cs` 與 `.github/workflows/build-and-release.yml` 內的版本值，再至 GitHub 的 [Actions 頁面](../../actions/workflows/build-and-release.yml) 手動觸發 **Build and Release** workflow。

#### 發佈參數說明

| 參數 | 說明 | 預設值 |
|------|------|--------|
| `release_type` | `release`（正式版）或 `prerelease`（測試版） | `prerelease` |
| `release_name` | 自訂發佈名稱（留空使用 `v5.9.2.1`） | 空 |

#### 版本控制機制

版本不再自動計算，也不使用 timestamp。Release tag 採固定四段式，例如 `v5.9.2.1`。

### 程式碼格式化

本專案使用 [CSharpier](https://csharpier.com/) 作為程式碼格式化工具。

```bash
# 安裝還原工具
dotnet tool restore

# 格式化所有 .cs 檔案
dotnet csharpier format .

# 檢查格式（不修改檔案）
dotnet csharpier check .
```

### 本地測試打包 (僅供開發除錯用)

若你需要在本地端測試打包流程，可使用以下指令。
*(⚠️ 注意：此產出的 `BeanfunClassic.exe` 僅供本地除錯，切勿手動上傳至 GitHub Release)*

```bash
build_win10.bat
```

產出檔案位於 `dist/BeanfunClassic.exe`。