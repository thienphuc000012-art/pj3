# Manga Intro — hướng dẫn chỉnh sửa

Mở `Assets/Scenes/MangaIntro.unity`, chuyển sang tab Game rồi nhấn Play để xem liên tục ba chương truyện có giọng đọc trong khoảng 112 giây. Nếu đang kéo thử playhead trong Timeline Editor, tắt Preview trước khi nhấn Play. Scene độc lập này dùng Canvas, TextMeshPro, Animator và Unity Timeline.

Trong lúc intro phát, bấm **Esc** hoặc **Space** để hiện chữ **Bỏ qua**. Giữ **Space** để viền trắng chạy quanh ô chữ. Thả sớm sẽ đặt lại viền, và chữ **Bỏ qua** tự ẩn sau 2 giây. Chỉnh thời gian giữ và thời gian hiện chữ ở `MangaIntro_Master → Manga Intro Skip → Hold Seconds / Prompt Seconds`. Chỉnh vị trí hoặc kiểu chữ trong `MangaCanvas → Skip Prompt`. Khi giữ đủ, Master Timeline kết thúc và màn hình về đen.

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
| 01 | 0,000 giây | 45,311 giây | 45,311 giây |
| 02 | 45,311 giây | 80,125 giây | 34,814 giây |
| 03 | 80,125 giây | 111,975 giây | 31,850 giây |

Các chương nối tiếp nhau, không chồng thời gian. Mỗi fade bắt đầu đen, mở rõ trong 0,3 giây, giữ trong suốt, rồi về đen trong 0,4 giây cuối. Control clip giữ Speed Multiplier = 1 để giọng đọc đúng tốc độ và cao độ; các animation reveal giữ tốc độ hình ảnh trước khi thêm giọng đọc. Director của `MangaIntro_Master` dùng **Update Method = DSP Clock**.

Khi dời cả chương, chọn đồng thời Control clip và fade clip tương ứng rồi kéo cả cặp để giữ chúng khớp nhau. Có thể dùng Inspector để nhập Start chính xác. Thứ tự phát theo vị trí Control clip từ trái sang phải. Giữ các clip tách nhau để mỗi lần chỉ kích hoạt một chương. Sau khi đổi vị trí, thứ tự hoặc độ dài các Control clip, chọn `Tools > Manga Intro > Sync Shared Fades To Chapter Clips` để căn lại fade theo từng chương. Công cụ ghép cặp theo binding gốc và asset animation fade, nên tên hiển thị của clip có thể thay đổi. Công cụ cũng điều chỉnh tốc độ clip fade cho vừa thời lượng mới. Kiểm tra lại đường cong nếu cần giữ chính xác 0,6 giây mở và 0,8 giây đóng.

## Sửa từng chương và thời gian đọc

Chọn gốc `Chapter_01`, `Chapter_02` hoặc `Chapter_03` trong Hierarchy để xem Timeline con. Play On Awake của cả ba director con phải tắt; Wrap Mode là Hold. Master sẽ bật đúng chương và truyền thời gian xuống bằng Control clip.

Để preview một chương độc lập trong Edit Mode, tạm bật duy nhất gốc chương đó trong Hierarchy, chọn director con và bật Timeline Preview rồi kéo playhead. Tắt Preview sau khi xem. Trước khi lưu scene, khôi phục cả ba gốc chương và toàn bộ `Panel_01`–`Panel_05` về trạng thái inactive. Khi chạy scene, Timeline sẽ bật chúng theo lịch; không bật cả ba chương để preview cùng lúc.

Mỗi panel có một Activation Track bind trực tiếp vào GameObject của panel. Thời điểm hiện đã được căn theo độ dài giọng đọc bên dưới; mọi Activation clip kéo dài đến cuối chương. Panel đã xuất hiện sẽ được giữ lại, tạo thành trang đủ năm khung. Post Playback của Activation Track là Inactive. Animation Track bind Animator của panel tạo fade-in qua CanvasGroup; nhóm Dialogue có Animator và CanvasGroup riêng để chỉnh nhịp xuất hiện chữ.

| Chương | Panel 01 | Panel 02 | Panel 03 | Panel 04 | Panel 05 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 01 | 0,250 | 17,635 | 26,380 | 30,517 | 36,838 |
| 02 | 0,250 | 6,043 | 11,380 | 19,153 | 22,153 |
| 03 | 0,250 | 10,831 | 15,472 | 21,913 | 24,913 |

Các mốc trong bảng là giây tính từ đầu Timeline con.

Track opacity của gốc chương có các clip `PageIn`, `HoldFullPage` và `PageFadeOut`. Kéo dài `HoldFullPage` để tăng thời gian đọc toàn trang, hoặc rút ngắn để tăng nhịp kể. Nếu muốn lặp đoạn giữ, dùng bản sao của clip `HoldFullPage` và đặt sát nhau; clip giữ chỉ cần duy trì alpha bằng 1. Không lặp cả Timeline con vì sẽ đưa các panel về lịch reveal đầu chương.

Sau khi đổi độ dài chương, đồng bộ đủ các vị trí sau:

1. Dời `PageFadeOut` tới cuối mới và đặt Fixed Duration của Timeline con tới cuối đó nếu đang dùng Fixed Length.
2. Kéo cuối cả năm Activation clip đến cuối chương mới; giữ nguyên đầu nếu không đổi nhịp reveal.
3. Đổi Duration của Control clip trong master; giữ Clip In bằng 0 và Speed Multiplier bằng 1.
4. Đổi fade clip của overlay để khớp hoàn toàn với Control clip, giữ fade ra ở cuối mới.
5. Dời các cặp Control/fade phía sau, không để các chương chồng nhau và kiểm tra thời lượng cuối master.

Để sửa đường cong, chọn Animation clip rồi mở cửa sổ Animation hoặc phần Curves trong Timeline. Chỉnh thuộc tính CanvasGroup Alpha từ 0 đến 1 cho reveal, từ 1 về 0 cho page fade. Giữ màu bản thân chữ là đen; chỉnh opacity bằng CanvasGroup để không làm mất tương phản của caption.

## Giọng đọc Panel / Speech

Chọn **MangaIntro_Master**, mở Timeline rồi mở nhóm **Dialogue Voices — DSP**. Bên trong có ba Audio Track **Chapter_01/02/03 — Dialogue Voice (Panel / Speech)**. Mỗi clip mang tên `Panel_XX / Speech_XX`, lấy từ `Assets/asset/intro/Chapter_XX/chapXdialog/Panel_XX/`. Toàn bộ 18 MP3 đã được gán: chương 1 có 7, chương 2 có 6, chương 3 có 5. Thời gian clip audio tính từ đầu master; các bảng thời gian panel phía trên vẫn tính từ đầu chương.

Giọng đọc phải nằm trên master dùng DSP Clock. Các director con vẫn dùng Manual để điều khiển hình ảnh; đặt audio vào Timeline con này khiến Control Track đồng bộ/tua audio lại theo từng khung hình và có thể gây tiếng rè, giật. Bản sửa chuyển nguyên các clip sang master, giữ đúng vị trí tương ứng và xóa track audio cũ khỏi Timeline con để không phát trùng.

Các Speech trong cùng panel đọc lần lượt, nghỉ 0,18 giây giữa hai clip. Panel tiếp theo chờ đoạn đọc kết thúc. Audio clip không loop, không bị cắt ngắn, không tăng tốc. Thay giọng bằng cách chọn clip và đổi Audio Clip trong Inspector; khi thay file dài hơn, dời các clip, reveal và cuối chương tương ứng để không chồng tiếng.

`Chapter_01/Panel_02/Speech_01` đang dùng bản thay mới có GUID `8773e6da4ecfbc847a87b289e80b4ea9` (hiện được đổi tên thành `speech_01.mp3`), dài 7,92 giây, phát từ 18,110 đến 26,030 giây. Panel_03 hiện ở 26,380 giây, nghỉ 0,35 giây sau lời đọc. Các panel phía sau, audio, fade và Chapter_02–03 đã dời sớm 3,912 giây so với bản cũ.

Mỗi gốc chương có đối tượng **Dialogue Voice** chứa AudioSource 2D, Volume = 0,9 và `GameAudioSourceBus = Voice`. Có thể chỉnh riêng Volume của nguồn này, hoặc dùng **Voice/Giọng nói** và **Master/Tổng** trong Options. Nhạc nền intro vẫn dùng phần Music đã có. Khi dừng hoặc bỏ qua master, giọng đọc cũng dừng.

`Chapter_02/Panel_04` có chữ nhưng chưa có MP3 được cung cấp nên hiện chỉ có chữ; có thể bổ sung clip vào track chương 2 trên master ở khoảng 64,939 giây (19,628 giây tính từ đầu chương), rồi dời Panel_05 nếu cần. `Chapter_03/Panel_04` là panel không lời thoại. Không tự tạo thêm lời đọc cho hai vị trí này.

Kiểm tra âm thanh trong Edit Mode bằng `Tools > Manga Intro > Validate Dialogue Voices`; kết quả ở `Tools/MangaIntroVoice/Validation.txt`. Công cụ kiểm tra audio nằm trên graph master DSP, Timeline con không có audio, binding thật, vị trí playhead khi seek, đủ 18 file, không chồng tiếng và dừng khi bỏ qua. Kết quả giải mã mẫu âm thanh nằm ở `Tools/MangaIntroVoice/AudioData.txt`; cả 18 file không có mẫu vượt ngưỡng toàn thang trong lần kiểm tra. Bản sao trước khi thêm giọng nằm trong `Tools/MangaIntroVoice/Before-20261117-121830765`; bản sao trước khi sửa DSP nằm trong `Tools/MangaIntroVoice/Before-DspScheduling-20261117-123800774`.

## Thay tranh và lời thoại

Các lớp tranh đang dùng **RawImage**: thay PNG tại ô **Texture** của RawImage trong nhóm thích hợp. Giữ tỷ lệ Width/Height của RectTransform theo ảnh nguồn, hoặc thêm AspectRatioFitter cho lớp đó khi cần tự giữ tỷ lệ. Chỉnh RectTransform để crop hoặc pan; Mask của panel sẽ giữ nội dung trong khung. Nếu đổi hình dạng khung, cập nhật các điểm chuẩn hóa trên MangaPolygonGraphic, giữ các đỉnh theo một chiều quanh biên và tránh cạnh tự cắt. Nhóm Effects có sẵn một nhịp sáng trắng nhẹ qua CanvasGroup; có thể thay lớp hình hoặc chỉnh clip Light trong Timeline con.

Sửa nội dung trong TMP Text bên dưới `Dialogue`. Riêng `Chapter_03/Panel_04` là cảnh bước qua cổng không có lời thoại theo bản truyện gốc; nhóm Dialogue vẫn có sẵn để chỉnh animation khi cần. Giữ font có hỗ trợ tiếng Việt, màu chữ đen và nền caption sáng. Kiểm tra dấu tiếng Việt, xuống dòng và cỡ chữ ở Game View; tránh dựa vào chữ nhỏ chỉ đọc được khi phóng to Scene View. Các lớp Artwork và caption là các đối tượng riêng nên có thể thay hoặc thêm chuyển động bằng Animation Track mà vẫn giữ cấu trúc truyện.

## Kiểm tra tự động và ảnh đối chiếu

Trong Edit Mode, chọn `Tools > Manga Intro > Validate Saved Intro`. Có thể yêu cầu một lần bằng cách tạo tệp trống `Library/MangaIntroValidation.request` khi Unity đang mở; validator chỉ chạy khi có yêu cầu hoặc chọn menu, không tự chạy khi reload code hay mở project.

Kết quả nằm tại `Library/MangaIntroValidation.result.txt`, gồm PASS/FAIL, các binding thực tế, mốc thời gian, số lần scrub và thời gian chạy. Ảnh PNG 1920×1080 nằm trong `Library/MangaIntroValidation/`: toàn trang của ba chương, năm bước reveal của chương 1 và một ảnh khoảng đen giữa chương. Hai ảnh toàn trang chương 1 ở 1280×1024 và 1080×1920 kiểm tra thêm CanvasScaler, tỷ lệ trang và vùng an toàn khi đổi hình dạng màn hình.

Validator đọc bản scene đã lưu bằng một preview scene riêng, đánh giá PlayableDirector và Control clip thật theo chiều xuôi, ngược và nhảy mốc. Nó kiểm tra chỉ một chương hoạt động, panel cũ vẫn giữ lại, toàn trang đủ năm panel trong đoạn đọc và mọi chương tắt sau Stop. Camera/Canvas được chuẩn bị chỉ trong preview để chụp UI; validator không lưu lại thay đổi, không mở đè scene hiện tại và không làm mất công việc chưa lưu. Hãy lưu thay đổi của intro trước khi kiểm tra vì kết quả phản ánh bản trên ổ đĩa.

Kiểm tra bố cục đọc lịch hiện panel, khoảng giữ trang và thời lượng từ các Timeline đã lưu, nên vẫn dùng được sau khi căn lại nhịp đọc. Ảnh tự động hỗ trợ kiểm tra bố cục; vẫn nên xem trọn intro trong Game View để đánh giá nhịp kể và khả năng đọc.
