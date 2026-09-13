# Peeklism

在 Windows 上按空白鍵即時預覽檔案 —— 桌面、檔案總管、開啟檔案對話框。

Peeklism 是獨立產品，與 [Flowlism](https://github.com/tavricccc/flowlism) 啟動器分開安裝、分開更新。兩者共用同一套視覺語言（WinUI 3、Mica、Fluent），但不共用行程，也沒有互相依賴。

## 現況

專案剛起步，目前只完成最底層、風險最高的一段：**判斷前景視窗屬於哪種 shell 介面，並讀出它目前選取的檔案**。

| 介面 | 偵測 | 讀取選取項目 |
| --- | --- | --- |
| 檔案總管 | 完成 | 完成 |
| 桌面 | 完成 | 完成 |
| 開啟檔案對話框 | 完成 | 待原生輔助 DLL |

檔案總管與桌面不需要注入程式碼：Explorer 會把自己的視窗註冊到行程外可見的 shell 視窗集合，`IShellWindows` 直接就能列舉。開啟檔案對話框屬於別的行程且不會註冊，必須把原生 DLL 載入對方執行緒才能讀取，這部分之後由 `Peeklism.Native` 處理。

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
