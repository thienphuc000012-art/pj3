# Unity crash – 16/11/2026, khoảng 21:34 (UTC+7)

Unity Editor: 6000.3.12f1, Windows 11 x64.

## Kết luận từ log và dump

Editor sập khi người dùng bấm quay lại/thoát Prefab Mode. Unity gọi dọn asset và duyệt các tham chiếu MonoBehaviour; lỗi native xảy ra trong Mono runtime.

Managed call chain:
`StageHeaderGUI → NavigateBack → SwitchToStage → UnloadUnusedAssetsImmediate → UnloadUnusedAssets`

Native stack:
`ProcessMonoBehaviour → MarkCallbackInstanceIDsAsRoot → MarkAllDependencies`

Cả crash.dmp của Unity và dump của Windows đều ghi mã `0xC0000005`, thao tác ghi vào địa chỉ `0x10`, trong `mono-2.0-bdwgc.dll` tại offset `0x26c00e`. Đây là lỗi truy cập bộ nhớ native; log không cung cấp tên script cụ thể gây hỏng tham chiếu.

Ngay trước lúc sập, Editor vừa import nhiều animation của DarknessSpider, quay ra khỏi Prefab Mode và bắt đầu lượt dọn asset tiếp theo. Chuỗi thao tác này là bối cảnh, chưa chứng minh prefab hoặc animation đó bị hỏng. Log ghi mức bộ nhớ Unity khoảng 6.67 GB ngay trước crash và không có thông báo OutOfMemory tại vị trí sập.

## Thiết lập liên quan và hướng thử

`ProjectSettings/EditorSettings.asset` đang có `m_ForceAssetUnloadAndGCOnSceneLoad: 1`.

Unity xác nhận thiết lập này ép dọn asset/GC khi tải scene hoặc thoát Prefab Mode. Có thể thử bỏ tick **Edit → Project Settings → Editor → Force GC on Scene Loads**, rồi theo dõi thao tác đóng prefab. Đây là workaround để tránh lượt dọn asset đang gây sập, chưa phải xác nhận sửa tận gốc. Tắt mục này có thể giữ asset/bộ nhớ lâu hơn giữa các scene/prefab. Không thay đổi thiết lập này trong lượt kiểm tra hiện tại.

Unity có issue Mono với stack `ProcessMonoBehaviour` tương tự, UUM-146655; chưa xác nhận project này gặp đúng issue đó, và trang issue đang ghi Open. Không khẳng định nâng phiên bản sẽ chữa được lỗi.

Nguồn chính thức:
- https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/editorsettings/forceassetunloadandgconsceneload
- https://issuetracker.unity.com/issues/23305/crash-on-monotraverseobjectinternal-when-opening-a-specific-scene

## Dữ liệu đã lưu

- CrashExcerpt.log: trích đoạn cuối log và native stack; không sao chép token khởi động.
- DumpMetadata.json: mã lỗi/module/offset từ cả hai dump, không sao chép nội dung bộ nhớ.

Nguồn gốc: `C:/Users/Phuc/AppData/Local/Temp/Unity/Editor/Crashes/Crash_2026-11-16_143449297/`.

Kiểm tra chỉ đọc log/dump và ghi báo cáo vào Tools; không thay đổi script, scene, prefab hoặc Project Settings, không chạy lại thao tác gây crash.
