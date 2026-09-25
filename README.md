# Peeklism

在 Windows 檔案總管或桌面選取檔案，按空白鍵看一眼；需要仔細閱讀時，再開啟完整查看器。

在檔案總管或桌面挑一個檔案，按空白鍵就能看圖片、播放影音、閱讀 PDF、Markdown 和文字，或先查看資料夾內容。按方向鍵換檔時，預覽也跟著換，不用一個個開啟應用程式。

想看得更仔細，可以從預覽開啟完整查看器：圖片能放大、拖移和旋轉；影音有播放進度與倍速；PDF 可以搜尋、選取文字和列印。查看器也支援拖放、多選及全螢幕。Peeklism 常駐系統匣，隨時可以暫停預覽。

從 [GitHub Releases](https://github.com/tavricccc/peeklism/releases/latest) 下載 `Peeklism.Setup.exe`。支援 Windows 11 build 26100 以上、x64。安裝檔包含共用與自帶 Windows App 執行環境兩種版型；安裝在目前使用者帳號，不需要管理員權限。安裝檔尚未簽章。

## 現況

在檔案總管按空白鍵是輕量預覽；使用「開啟檔案 → Peeklism」、點預覽中的「完整查看」，或從系統匣選「開啟完整查看器」，會開啟獨立視窗。開啟檔案對話框的預覽與設定介面仍未完成。

| 介面 | 偵測 | 讀取選取項目 |
| --- | --- | --- |
| 檔案總管 | 完成 | 完成 |
| 桌面 | 完成 | 完成 |
| 開啟檔案對話框 | 完成 | 待原生輔助 DLL |

| 項目 | 狀態 |
| --- | --- |
| 低階鍵盤 hook：空白鍵開關預覽、Esc 收回、Enter 交還給 Explorer | 完成 |
| 不奪取焦點的預覽視窗（`WS_EX_NOACTIVATE`、置頂、圓角與系統背景） | 完成 |
| 方向鍵換選取時只換內容，視窗不重新置中或改變大小 | 完成 |
| 圖片、影片、音訊、純文字、Markdown、PDF、資料夾預覽 | 完成 |
| 完整查看器：原圖縮放／拖移／旋轉、影音控制、完整 PDF、Markdown／文字 | 完成 |
| 查看器：多選、拖放、同類檔案切換、全螢幕、快捷鍵 | 完成 |
| Open with 檔案關聯（不修改預設程式） | 完成 |
| 系統匣：完整查看器、暫停、登入時啟動、關於、結束 | 完成 |
| 單一實例與安裝／更新／解除安裝 | 完成 |
| 開啟檔案對話框的預覽（`Peeklism.Native`） | 未開始 |
| 設定介面（目前所有選項都在系統匣選單裡） | 未開始 |
| GitHub Releases 安裝檔 | 已提供 |
| 程式碼簽章與專案授權 | 未設定 |

檔案總管與桌面不需要注入程式碼：Explorer 會把自己的視窗註冊到行程外可見的 shell 視窗集合，`IShellWindows` 直接就能列舉。開啟檔案對話框屬於別的行程且不會註冊，必須把原生 DLL 載入對方執行緒才能讀取，這部分之後由 `Peeklism.Native` 處理。

## 完整查看器

| 格式 | 完整模式 |
| --- | --- |
| 圖片 | 原始解析度解碼、100% 實體像素、符合視窗、縮放、拖移、旋轉；支援 SVG |
| 影片／音訊 | 播放／暫停、進度、音量、倍速、循環、全螢幕；使用 Windows 解碼器 |
| PDF | WebView2 原生 PDF 閱讀器：全部頁面、跳頁、縮放、搜尋、文字選取、列印 |
| Markdown | Markdig 解析標題、表格、巢狀清單、工作清單、程式碼區塊；排版／原始碼切換 |
| 文字／程式碼 | 讀取完整檔案，不沿用預覽的 256 KB 截斷；支援 UTF-8 與帶 BOM 的 Unicode |

`Ctrl+O` 開啟檔案、`Alt+←/→` 切換檔案、`Ctrl+1` 顯示原圖、`Ctrl+0` 符合視窗、`Ctrl+R` 重新載入、`F11` 全螢幕。文件內可用 `Ctrl+F` 搜尋，影音用空白鍵播放／暫停。查看器內的空白鍵不會關閉視窗。

```powershell
Peeklism.App.exe --viewer
Peeklism.App.exe --viewer -- "C:\文件\報告.pdf"
Peeklism.App.exe "C:\圖片\照片.png"
```

安裝程式會把 Peeklism 加入支援格式的「開啟檔案」清單，不修改 Windows 的預設程式。也可直接在「選擇電腦上的應用程式」指定 `Peeklism.App.exe`。

PDF 與文件需要 Microsoft Edge WebView2 Runtime。HEIC、AVIF、HEVC 等格式是否可讀取，取決於已安裝的 Windows 解碼器。Markdown 不執行 HTML／JavaScript／MDX 元件，不載入遠端圖片；本機圖片限於文件所在資料夾及其子資料夾。文字超過 64 MiB、圖片超過 1.6 億像素時會明確拒絕，不會假裝已讀完整份檔案。

架構、安全限制與驗證步驟見 [docs/viewer.md](docs/viewer.md)。

## 系統匣

程式常駐系統匣，右鍵選單可暫停預覽、設定登入 Windows 時啟動、查看版本或結束。暫停時鍵盤 hook 會被卸下，而不只是忽略按鍵。

開機自啟用的是目前使用者的 Run 登錄機碼，不需要系統管理員權限。Peeklism 只有在執行中才有用——鍵盤 hook 沒裝上之前，空白鍵什麼也不會發生，而一個要先自己去啟動的預覽工具，沒有人會想到要用。

## 安裝程式

```
pwsh -File scripts/Publish-Installer.ps1
```

產出是單一檔案 `artifacts/installer/Peeklism.Setup.exe`，複製走就能用。預設安裝到 `%LocalAppData%\Programs\Peeklism`。上一版安裝程式會移到 `artifacts/installer/history`。

腳本需要 **PowerShell 7**（`pwsh`）；Windows PowerShell 5.1 缺少它用到的 API。

## 手動驗證

```
dotnet run --project tools/Peeklism.Probe
```

讓它在背景跑，然後切換到檔案總管或桌面點選檔案。每次前景視窗或選取項目變化都會印出一行。

## 共用的 Windows App 執行環境

WinUI 不再複製進安裝資料夾，而是來自 Windows 集中保管的 MSIX framework package——整台機器一份，Downlism、Flowlism、Peeklism 指向同一份。三個各帶一份一樣的 145 MB，在磁碟上是三份，同時開著的時候在記憶體裡也是三份。

安裝程式只有一個檔案，裡面有兩種版型：

| 裝到機器上的是 | 需要什麼 |
| --- | --- |
| **共用版型** | 機器上登錄一份共用的 Windows App 執行環境（元件已在安裝程式裡，不需連網） |
| **自帶版型** | 什麼都不需要，整套 SDK 都在安裝資料夾裡 |

兩種版型都在同一個 `Peeklism.Setup.exe` 裡，裝哪一種由安裝程式自己決定。自帶版型就是共用版型加上那套 SDK 二進位檔，封裝時逐檔比對雜湊，相同的只存一份，所以同檔不等於兩倍大。

登錄由 `Peeklism.Bootstrap` 在安裝介面啟動之前完成——安裝介面自己就是 WinUI，不可能是登錄它自己所需元件的那個東西。不需要系統管理員，也不需要開發人員模式：套件是微軟簽章的 Store 元件。已經登錄過就直接用共用版型，什麼都不問；沒有才詢問，而使用者拒絕、或機器不允許登錄共用元件時，改裝自帶版型繼續走完。

舊的自帶版型安裝升級到共用版型時會自動換版型：安裝紀錄裡不再存在的檔案會被移除。這是必要的，`Microsoft.UI.Xaml.dll` 留在執行檔旁邊的載入順序高於共用套件。

取捨的完整紀錄見 Downlism 的 `docs/tech-selection.md` §8.9。

## 建置

需要 .NET 10 SDK 與 Windows 11 build 26100+、x64。

```
dotnet build Peeklism.slnx
dotnet test Peeklism.slnx
```
