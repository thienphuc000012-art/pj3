# Manga Intro — hướng dẫn chỉnh sửa

Mở `Assets/Scenes/MangaIntro.unity`, rồi nhấn Play để xem liên tục ba chương truyện trong khoảng 100,9 giây. Scene độc lập này dùng Canvas, TextMeshPro, Animator và Unity Timeline. Không có nút Next, Close hoặc Replay; không cần mua thêm asset.

## Các tệp chính

| Tệp / đối tượng | Vai trò |
| --- | --- |
| `Assets/Scenes/MangaIntro.unity` | Scene mở đầu hoàn chỉnh |
| `MangaIntro_Master` | PlayableDirector chính; Play On Awake bật, Wrap Mode None |
| `Assets/MangaIntro/Timelines/MangaIntro_Master.playable` | Timeline chính, điều khiển lần lượt ba chương |
| `Assets/MangaIntro/Timelines/Chapter_01.playable` | Timeline chương 1 |
| `Assets/MangaIntro/Timelines/Chapter_02.playable` | Timeline chương 2 |
| `Assets/MangaIntro/Timelines/Chapter_03.playable` | Timeline chương 3 |
| `MangaCanvas/Chapter_01`, `Chapter_02`, `Chapter_03` | Gốc riêng của mỗi chương, có PlayableDirector, Animator và CanvasGroup |
| `MangaCanvas/FadeOverlay` | Image đen phủ toàn màn hình; Animator và CanvasGroup cho chuyển cảnh |

Mỗi chương có cấu trúc `PageSafeArea/Page/Panel_01` đến `Panel_05`. Trong từng panel có các nhóm `Background`, `Character`, `Relic`, `Effects`, `Dialogue` để thay tranh, nhân vật, cổ vật, hiệu ứng và lời thoại riêng. Các Graphic trong một panel được cắt theo hình đa giác bằng MangaPolygonGraphic và UI Mask. Giữ các lớp này bên trong panel để tranh và hiệu ứng không tràn ra ngoài khung.

`PageSafeArea` tạo khoảng trống an toàn quanh trang. `Page` giữ tỷ lệ bằng AspectRatioFitter; CanvasScaler co giãn theo màn hình. Khi đổi bố cục, nên kiểm tra cả 16:9 và một màn hình hẹp hơn để chữ không chạm viền. Marker `PreserveAuthoredUI` trên MangaCanvas bảo vệ màu đen trắng và màu chữ đã thiết kế khi hệ thống giao diện chung áp dụng theme.

## Xem và sửa Timeline chính

1. Mở `Window > Sequencing > Timeline` và chọn `MangaIntro_Master` trong Hierarchy.
2. Control Track có ba Control clip. Trong Inspector của từng clip, Source Game Object phải trỏ đúng `Chapter_01`, `Chapter_02` hoặc `Chapter_03`.
3. Bật Control Activation và Control Playable Directors. Tắt Control Particle Systems, Control ITimeControl và Search Hierarchy. Không gán prefab. Post Playback là Inactive; Director On Clip End là Pause Director.
4. Animation Track còn lại bind Animator của `FadeOverlay`, với ba clip fade có cùng đầu và cuối như ba Control clip.

| Chương | Bắt đầu trong master | Kết thúc | Độ dài |
| --- | ---: | ---: | ---: |
| 01 | 0,0 giây | 33,5 giây | 33,5 giây |
| 02 | 33,7 giây | 67,2 giây | 33,5 giây |
| 03 | 67,4 giây | 100,9 giây | 33,5 giây |

Khoảng cách 0,2 giây giữa hai chương giữ màn hình đen và tránh hai chương cùng hiện. Mỗi fade bắt đầu đen, mở rõ trong 0,6 giây, giữ trong suốt, rồi về đen trong 0,8 giây cuối.

Khi dời cả chương, giữ Ctrl và chọn đồng thời Control clip cùng fade clip tương ứng; kéo cả cặp để giữ chúng khớp nhau. Có thể dùng Inspector để nhập Start chính xác. Giữ ba Control clip theo thứ tự và không chồng lấn. Sau khi đổi vị trí hoặc độ dài các Control clip, chọn `Tools > Manga Intro > Sync Shared Fades To Chapter Clips` để căn lại ba fade clip theo các khoảng chương hiện tại. Công cụ cũng điều chỉnh tốc độ clip fade cho vừa thời lượng mới; kiểm tra lại đường cong nếu cần giữ chính xác 0,6 giây mở và 0,8 giây đóng. Sau khi đổi thời lượng, kiểm tra lại điểm bắt đầu chương sau, khoảng đen và thời điểm fade; không chỉ kéo một đầu của Control clip.

## Sửa từng chương và thời gian đọc

Chọn gốc `Chapter_01`, `Chapter_02` hoặc `Chapter_03` trong Hierarchy để xem Timeline con. Play On Awake của cả ba director con phải tắt; Wrap Mode là Hold. Master sẽ bật đúng chương và truyền thời gian xuống bằng Control clip.

Để preview một chương độc lập trong Edit Mode, tạm bật duy nhất gốc chương đó trong Hierarchy, chọn director con và bật Timeline Preview rồi kéo playhead. Tắt Preview sau khi xem. Trước khi lưu scene, khôi phục cả ba gốc chương và toàn bộ `Panel_01`–`Panel_05` về trạng thái inactive. Khi chạy scene, Timeline sẽ bật chúng theo lịch; không bật cả ba chương để preview cùng lúc.

Mỗi panel có một Activation Track bind trực tiếp vào GameObject của panel. Clip bắt đầu lần lượt ở 0,5; 6,5; 12,5; 18,5; 24,5 giây và kéo dài đến 33,5 giây. Panel đã xuất hiện sẽ được giữ lại, tạo thành trang đủ năm khung. Post Playback của Activation Track là Inactive. Animation Track bind Animator của panel tạo fade-in qua CanvasGroup; nhóm Dialogue có Animator và CanvasGroup riêng để chỉnh nhịp xuất hiện chữ.

Track opacity của gốc chương có các clip `PageIn`, `HoldFullPage` và `PageFadeOut`. Kéo dài `HoldFullPage` để tăng thời gian đọc toàn trang, hoặc rút ngắn để tăng nhịp kể. Nếu muốn lặp đoạn giữ, dùng bản sao của clip `HoldFullPage` và đặt sát nhau; clip giữ chỉ cần duy trì alpha bằng 1. Không lặp cả Timeline con vì sẽ đưa các panel về lịch reveal đầu chương.

Sau khi đổi độ dài chương, đồng bộ đủ các vị trí sau:

1. Dời `PageFadeOut` tới cuối mới và đặt Fixed Duration của Timeline con tới cuối đó nếu đang dùng Fixed Length.
2. Kéo cuối cả năm Activation clip đến cuối chương mới; giữ nguyên đầu nếu không đổi nhịp reveal.
3. Đổi Duration của Control clip trong master; giữ Clip In bằng 0 và Speed Multiplier bằng 1.
4. Đổi fade clip của overlay để khớp hoàn toàn với Control clip, giữ fade ra ở cuối mới.
5. Dời các cặp Control/fade phía sau, giữ khoảng đen 0,2 giây và kiểm tra thời lượng cuối master.

Để sửa đường cong, chọn Animation clip rồi mở cửa sổ Animation hoặc phần Curves trong Timeline. Chỉnh thuộc tính CanvasGroup Alpha từ 0 đến 1 cho reveal, từ 1 về 0 cho page fade. Giữ màu bản thân chữ là đen; chỉnh opacity bằng CanvasGroup để không làm mất tương phản của caption.

## Thay tranh và lời thoại

Các lớp tranh đang dùng **RawImage**: thay PNG tại ô **Texture** của RawImage trong nhóm thích hợp. Giữ tỷ lệ Width/Height của RectTransform theo ảnh nguồn, hoặc thêm AspectRatioFitter cho lớp đó khi cần tự giữ tỷ lệ. Chỉnh RectTransform để crop hoặc pan; Mask của panel sẽ giữ nội dung trong khung. Nếu đổi hình dạng khung, cập nhật các điểm chuẩn hóa trên MangaPolygonGraphic, giữ các đỉnh theo một chiều quanh biên và tránh cạnh tự cắt. Nhóm Effects có sẵn một nhịp sáng trắng nhẹ qua CanvasGroup; có thể thay lớp hình hoặc chỉnh clip Light trong Timeline con.

Sửa nội dung trong TMP Text bên dưới `Dialogue`. Riêng `Chapter_03/Panel_04` là cảnh bước qua cổng không có lời thoại theo bản truyện gốc; nhóm Dialogue vẫn có sẵn để chỉnh animation khi cần. Giữ font có hỗ trợ tiếng Việt, màu chữ đen và nền caption sáng. Kiểm tra dấu tiếng Việt, xuống dòng và cỡ chữ ở Game View; tránh dựa vào chữ nhỏ chỉ đọc được khi phóng to Scene View. Các lớp Artwork và caption là các đối tượng riêng nên có thể thay hoặc thêm chuyển động bằng Animation Track mà vẫn giữ cấu trúc truyện.

## Kiểm tra tự động và ảnh đối chiếu

Trong Edit Mode, chọn `Tools > Manga Intro > Validate Saved Intro`. Có thể yêu cầu một lần bằng cách tạo tệp trống `Library/MangaIntroValidation.request` khi Unity đang mở; validator chỉ chạy khi có yêu cầu hoặc chọn menu, không tự chạy khi reload code hay mở project.

Kết quả nằm tại `Library/MangaIntroValidation.result.txt`, gồm PASS/FAIL, các binding thực tế, mốc thời gian, số lần scrub và thời gian chạy. Ảnh PNG 1920×1080 nằm trong `Library/MangaIntroValidation/`: toàn trang của ba chương, năm bước reveal của chương 1 và một ảnh khoảng đen giữa chương. Hai ảnh toàn trang chương 1 ở 1280×1024 và 1080×1920 kiểm tra thêm CanvasScaler, tỷ lệ trang và vùng an toàn khi đổi hình dạng màn hình.

Validator đọc bản scene đã lưu bằng một preview scene riêng, đánh giá PlayableDirector và Control clip thật theo chiều xuôi, ngược và nhảy mốc. Nó kiểm tra chỉ một chương hoạt động, panel cũ vẫn giữ lại, toàn trang đủ năm panel trong đoạn đọc và mọi chương tắt sau Stop. Camera/Canvas được chuẩn bị chỉ trong preview để chụp UI; validator không lưu lại thay đổi, không mở đè scene hiện tại và không làm mất công việc chưa lưu. Hãy lưu thay đổi của intro trước khi kiểm tra vì kết quả phản ánh bản trên ổ đĩa.

Các kiểm tra mốc chuẩn dùng thời lượng mặc định trong bảng trên. Khi chủ động đổi thiết kế thời gian, cập nhật các giá trị mong đợi trong `Assets/Editor/MangaIntroValidation.cs` để báo cáo tiếp tục phản ánh lịch mới. Ảnh tự động hỗ trợ kiểm tra bố cục; vẫn nên xem trọn intro trong Game View để đánh giá nhịp kể và khả năng đọc.
