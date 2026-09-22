# 完整查看器

空白鍵預覽和完整查看器使用不同視窗，也使用不同的行程生命週期。查看器不安裝鍵盤 hook，不參與背景程式的單一實例 gate，因此背景預覽執行中仍可用「開啟檔案 → Peeklism」開啟多個文件。

## 入口與操作

- 無引數或 `--background`：原本的系統匣／空白鍵預覽。
- `--viewer`：開啟空查看器，可拖放檔案或按 Ctrl+O。
- 檔案路徑引數，或 `--viewer -- "完整路徑"`：直接使用完整模式。引數由 `Environment.GetCommandLineArgs()` 取得，不自行依空白拆字串。
- 預覽的「完整查看」與系統匣的「開啟完整查看器」都透過 `ViewerLauncher` 啟動新行程。

開啟一個檔案時，上一個／下一個會列出同資料夾、同種類的檔案，以檔名排序。一次多選或拖入多個檔案時，則以這批檔案作為清單。完整模式的空白鍵只控制影音，不關閉視窗；Esc 只離開全螢幕。

| 快捷鍵 | 操作 |
| --- | --- |
| Ctrl+O | 開啟檔案，可多選 |
| Alt+←／→ | 上一個／下一個檔案 |
| Ctrl+1 | 圖片 100% 實體像素，依螢幕 DPI 換算 |
| Ctrl+0 | 圖片符合視窗；文件焦點內使用瀏覽器的重設縮放 |
| Ctrl+滾輪 | 縮放 |
| 圖片雙擊 | 切換符合視窗／原始大小 |
| 滑鼠拖曳圖片 | 平移 |
| Ctrl+F | 文件焦點內搜尋；PDF 也有搜尋按鈕 |
| Ctrl+R | 重新載入原檔 |
| F11／Esc | 全螢幕／返回 |
| 空白鍵 | 影音播放／暫停 |
| ←／→ | 影音倒轉／快轉 5 秒 |

## 各格式的讀取方式

圖片使用 WinUI `BitmapImage`，明確指定原始解碼寬度與 `DecodePixelType.Physical`，不使用縮圖或依視窗大小降採樣。畫面縮放只改 layout，沒有把低解析度預覽放大。SVG 讀取尺寸或 viewBox，再交給原生 SVG 解碼器；XML 不允許 DTD 或外部實體。旋轉只影響畫面，不寫回原檔。

影片與音訊使用 Windows `MediaPlayer`，提供原生進度、音量、倍速控制與循環開關。完整模式有聲音；預覽仍預設靜音。換檔或關窗會解除來源、處置播放器與 media source，避免留下聲音或檔案鎖定。

PDF 交給 WebView2 內建閱讀器，不再把前 25 頁轉成固定寬度圖片。頁碼、搜尋、文字選取、縮放、列印和密碼提示由該閱讀器處理；程式隱藏儲存／另存按鈕並取消下載，不提供修改原檔的功能。WebView2 本身的版本會影響工具列外觀。

Markdown 使用 Markdig 的擴充解析器，涵蓋 GFM 表格、工作清單、巢狀清單與程式碼區塊。文字支援 UTF-8、帶 BOM 的 UTF-16／UTF-32。文字讀取與 Markdown 解析不在 UI 執行緒做；HTML 透過虛擬網址的 resource response 提供，沒有 `NavigateToString` 的大小限制，也不會把文件寫入暫存 HTML。

## 安全與資源限制

- Markdown 禁用原始 HTML；CSP 禁用 script、frame、表單與遠端資源。MDX 元件不執行，數學式不載入遠端排版程式。
- 外部 HTTP／HTTPS／mailto 連結須確認後才交給預設程式。本機檔案連結與特殊協定不執行；開其他檔案請用 Ctrl+O。
- Markdown 本機圖片只允許文件資料夾以下的 PNG、JPEG、GIF、WebP、BMP、ICO、AVIF。拒絕路徑穿越、NTFS alternate data stream、符號連結及 junction；不把整個資料夾映射給瀏覽器。
- 內嵌圖片單張上限 32 MiB，總讀取預算 128 MiB，最多 255 個圖片 response；受阻擋的圖片保留替代文字。這個限制不套用到直接開啟的圖片。
- 文字上限 64 MiB，讀取中也檢查檔案是否持續長大。超限會顯示錯誤，不顯示部分文件冒充完整內容。
- 點陣圖超過 1.6 億像素時拒絕完整解碼。SVG 顯示用點陣表面上限為最長邊 8192 像素、總計 3200 萬像素；縮放時會重新要求適合的表面大小。
- HEIC／HEIF、AVIF、HEVC 和其他格式取決於 Windows 已安裝的解碼器，不包含額外的萬用影音解碼包。
- WebView2 的使用者資料位於 `%LocalAppData%\Peeklism\WebView2`，不寫入原檔所在資料夾。

## 檔案關聯與安裝

安裝程式在 HKCU 註冊 `Applications\Peeklism.App.exe`、`Peeklism.Viewer` ProgID，以及各副檔名的 `OpenWithProgids`。不寫入副檔名預設值，也不碰 `UserChoice`。命令格式為：

```text
"<安裝目錄>\Peeklism.App.exe" --viewer -- "%1"
```

註冊寫入失敗時回復先前值。解除安裝只移除指向該安裝位置的關聯，保留其他應用程式。每個查看器行程都註冊維護用 shutdown signal，更新程式可先請它們釋放檔案。

PDF 與文件需要 Microsoft Edge WebView2 Runtime；目前安裝包不另外攜帶 WebView2。Windows 11 通常已有此執行環境，缺少時會在文件查看器顯示錯誤與所需元件名稱。

打包共用版與自帶版時，各專案會先 clean 再 publish。WinUI 的增量 PRI 建置不會完整追蹤 `WindowsAppSDKSelfContained` 切換；直接連續 publish 可能沿用不含 theme resources 的 PRI，讓自帶版在第一個視窗之前失敗。

## 介面延續

這次沿用現有 WinUI 3／Fluent 元件、Segoe UI、系統色彩與品牌圖示，沒有替換視覺識別。完整視窗採原生標題列、可溢出的 CommandBar、中央內容區與底部狀態列。窄視窗將較少用的命令移入「其他選項」；檔案路徑可截短，但滑鼠提示仍有完整路徑。Markdown 使用 80ch 閱讀寬度，程式碼與表格可水平捲動。

## 驗證

```powershell
dotnet build Peeklism.slnx
dotnet test tests/Peeklism.Tests
pwsh -File scripts/New-ViewerFixtures.ps1
pwsh -File scripts/Publish-Installer.ps1
```

`New-ViewerFixtures.ps1` 產生 6000×4000 圖片、橫向 SVG、40 頁可搜尋 PDF、超過 256 KB 的文字、含本機圖片的 Markdown、靜音 WAV 和損壞的 PDF。輸出在 Git 忽略的 `artifacts/viewer-fixtures`，不碰使用者文件。

單元測試覆蓋入口引數、格式分類、Markdown 解析／HTML 防護、本機圖片路徑限制、完整 Unicode 讀取、大小限制、取消，以及隔離登錄樹中的關聯註冊／解除安裝。

發行前還應在桌面確認：

1. 背景預覽已啟動時，開啟兩個完整查看器；關閉其中一個不影響另一個或背景預覽。
2. 圖片切換 100%、平移、旋轉和符合視窗；縮窄視窗後，溢出命令仍可使用。
3. PDF 跳到第 40 頁、搜尋 `END-PAGE-40`、選取文字；密碼 PDF 仍能輸入密碼。
4. Markdown 本機圖片載入、原始碼切換、搜尋、內部錨點與外部連結確認；惡意 script 不執行。
5. 影音播放、暫停、拖進度、倍速、循環；換檔和關窗後不殘留聲音。
6. 拖放、多選、檔案不存在、檔案損壞、超限、快速換檔與載入中關窗。
7. 安裝後以 Explorer「開啟檔案」啟動；預設程式保持不變，解除安裝只清掉 Peeklism 的登錄。

本次直接驗證（未委派獨立審查）：Debug／Release 的 46 個測試通過；Windows 11 下已檢查桌面與 640 像素寬視窗、原圖 100%、旋轉後完整構圖、SVG 長寬比、PDF 第 40 頁、Markdown 本機圖片與原始碼、300,017 字元的完整文字、缺檔錯誤，以及 MP4／WAV 原生播放控制。背景行程與多個查看器可同時執行，維護用 shutdown signal 可正常結束它們。最終安裝包解出後，共用版 254 個與自帶版 534 個 manifest 項目的 SHA-256 均相符，兩種版型都可啟動與關閉。

沒有把所有選用解碼器、密碼 PDF、螢幕閱讀器、Explorer 右鍵端到端安裝流程或所有混合 DPI 配置列為已驗證。
