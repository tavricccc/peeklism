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

程式常駐系統匣，右鍵選單可暫停預覽、查看版本或結束。暫停時鍵盤 hook 會被卸下，而不只是忽略按鍵。

## 安裝程式

```
pwsh -File scripts/Publish-Installer.ps1
```

產出在 `artifacts/installer/current`：最外層只有 `Peeklism.Setup.exe`，其餘檔案在 `resources` 子資料夾，兩者必須一起保留。預設安裝到 `%LocalAppData%\Programs\Peeklism`。上一版安裝程式會保留在 `artifacts/installer/history`。

腳本需要 **PowerShell 7**（`pwsh`）；Windows PowerShell 5.1 缺少它用到的 API。

## 手動驗證

```
dotnet run --project tools/Peeklism.Probe
```

讓它在背景跑，然後切換到檔案總管或桌面點選檔案。每次前景視窗或選取項目變化都會印出一行。

## 建置

需要 .NET 10 SDK 與 Windows 11 build 26100+、x64。

```
dotnet build Peeklism.slnx
dotnet test Peeklism.slnx
```
