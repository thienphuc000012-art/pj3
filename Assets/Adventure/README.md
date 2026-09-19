# Hệ thống party và phiêu lưu — mapgame

## Chơi thử

Mở `Assets/Scenes/mapgame.unity` rồi Play.

- **P:** mở/đóng Main Menu. Chọn Party hoặc Inventory ở cột trái. Preview party bên phải chỉ để xem.
- **Inventory trong Main Menu:** rương đồ dạng lưới 4 cột, tối thiểu 16 ô. Phím B không còn mở riêng rương đồ.
- **E:** tương tác với vật phẩm, rương, điểm nghỉ hoặc quái ở gần. Có thể bấm chuột trái khi đứng gần quái để bắt đầu trận.
- **Esc:** đóng menu. Menu tạm dừng gameplay và mở chuột.
- Thanh EXP ở góc trái hiển thị tiến độ của thành viên đứng đầu party. EXP nhận được chia đều theo cùng giá trị cho tất cả thành viên.

`MapGame Systems` trong scene đang bật **Create Starter Interactions**. Khi Play, hệ thống tạo gần vị trí bắt đầu: điểm nghỉ màu xanh, thảo dược, rương, vùng khám phá và một quái dùng prefab chiến đấu. Đây là các điểm mẫu để thử luồng hoàn chỉnh; có thể tắt tùy chọn này khi đặt nội dung thật.

Mỗi cấp cần `100 + (level - 1) × 50` EXP; giới hạn cấp 100. Lên cấp nhận 3 điểm chỉ số và 1 điểm kỹ năng. Chỉ nâng cấp ở điểm nghỉ: HP +10, ATK +2, DEF +1, crit +2 điểm phần trăm hoặc crit damage +5 điểm phần trăm. Thuộc tính dùng bản nháp +/−; chỉ trừ điểm và áp dụng khi bấm Chấp nhận nâng cấp. Đổi thành viên hoặc rời trang sẽ hủy bản nháp. Kỹ năng chỉ mở khóa một lần, không tăng damage/heal/buff; vào trận chỉ mang theo kỹ năng đã mở.

## Điểm nghỉ, lưu game và chế tạo

Đến điểm nghỉ, nhấn E rồi chọn **Nghỉ ngơi • Hồi đầy máu • Lưu game** để đặt checkpoint và hồi sinh/hồi đầy máu cả party. Hai khu vực nâng cấp/chế tạo chỉ cho thao tác khi nhân vật ở gần điểm nghỉ.

Công thức mẫu: 2 thảo dược + 1 quặng = 1 thuốc hồi 40 HP. Thuốc dùng được trên map và trong trận, trừ đúng số lượng trong cùng rương đồ. Thuốc không hồi sinh người đã chết; hãy nghỉ ngơi.

Dữ liệu lưu tại `Application.persistentDataPath/adventure-save-v1.json`; lần ghi trước giữ trong `.bak`. Tự đọc khi vào mapgame. Thao tác nhận thưởng, dùng đồ, đổi thứ tự và nâng cấp/chế tạo cũng lưu tiến độ. Vị trí checkpoint chỉ thay khi bấm nghỉ/lưu. Đổi dữ liệu prefab hoặc thứ tự Starting Party sau khi đã có save cần xem xét tương thích save cũ; `prefabIndex` là danh tính thành viên.

## Đặt thêm nội dung trên map

Gắn **WorldInteraction** vào object muốn tương tác. Chọn Kind:

- **Pickup:** nhặt vật phẩm trong Rewards.
- **Chest:** nhận Rewards và Experience khi mở rương.
- **Discovery:** tự nhận Experience khi bước vào Range.
- **RestPoint:** mở menu nghỉ/lưu, nâng cấp, chế tạo.
- **Encounter:** điền các prefab có BattleUnit vào **Enemy Prefabs**; một phần tử cho mỗi quái trong nhóm. Khi thắng nhóm này mới nhận Rewards và Experience.

Điền tên hiển thị, khoảng cách tương tác và ID vật phẩm đúng với CampaignConfig. Instance được nhận diện theo scene + Persistent ID + đường dẫn hierarchy để các bản sao prefab không dùng chung trạng thái. Giữ nguyên hierarchy và ID của nội dung đã phát hành nếu muốn tiếp tục đọc trạng thái thu thập trong save cũ.

Sau khi tạo thêm object bằng code trong runtime, gọi `CampaignSession.Instance.RefreshWorld()` để cập nhật danh sách tương tác. Với object đặt sẵn trong scene, hệ thống tự tìm khi vào map.

Vật phẩm, công thức, party ban đầu và quy tắc hồi sinh được cấu hình trong `Assets/Resources/Adventure/CampaignConfig.asset`. Các prefab party/quái mẫu nằm trong `Assets/Adventure/Prefabs`, được lấy từ nhân vật đã cấu hình trong combattest; giữ các component BattleUnit, BattleUnit_AnimationEvents, Animator và các animation event để trận đấu vận hành.

## Luồng chiến đấu

Tương tác Encounter → lưu vị trí quay lại trong bộ nhớ → mở combattest → ẩn các nhân vật test có sẵn → instantiate prefab party và quái tại Player Slots/Enemy Slots. Thứ tự party lấy từ menu P. HP, chỉ số nâng cấp, kỹ năng đã mở khóa và túi đồ được chuyển sang trận.

Slot/camera bổ sung được tạo nếu nhóm quái vượt số slot hiện có; nên đặt sẵn slot và camera phù hợp trong combattest để có bố cục đẹp cho nhóm lớn. Party HUD hiện dùng các ô có sẵn trong AdvancedUIManager; cấu hình thêm ô nếu tăng số thành viên vượt số ô hiện tại.

- **Thắng:** lưu HP còn lại, nhận EXP và đồ, đánh dấu nhóm quái đã bị tiêu diệt rồi về đúng vị trí trước trận. Nhóm quái không xuất hiện lại khi tải map/save.
- **Thua:** không nhận thưởng, về checkpoint gần nhất và hồi đầy máu. Quái vẫn tồn tại. Nếu chưa lưu tại điểm nghỉ, dùng vị trí bắt đầu map.
- Chạy trực tiếp combattest vẫn sử dụng đội hình test cũ.

## Kiểm tra

`Tools > Adventure > Validate Campaign` trong Unity kiểm tra prefab, owner animation events, mesh, công thức, mốc EXP, save roundtrip, build scenes và battle slots. Các kiểm tra dữ liệu độc lập có trong `Tools/AdventureTests`; chạy `python Tools/AdventureTests/run_tests.py` tại thư mục dự án (cần .NET 9 SDK).

Checklist Play Mode cần thử:

1. P/B mở đóng; player và camera không nhận phím khi menu mở; đổi hai thành viên rồi kiểm tra vị trí spawn trong trận.
2. Nhặt thảo dược, mở rương, đi vào vùng khám phá; quay lại không nhận thưởng lần hai.
3. Nghỉ/lưu, xem trước chỉ số, chấp nhận nâng thuộc tính và mở khóa một kỹ năng; craft thiếu nguyên liệu không trừ đồ, craft đủ trừ đúng số lượng.
4. Vào trận, dùng thuốc một lần; kiểm tra rương giảm 1 và HP tăng tối đa 40.
5. Thắng: về map đúng chỗ, HP còn lại đúng, EXP/đồ được cộng một lần, quái biến mất.
6. Thua: về checkpoint đầy máu, không nhận thưởng, quái vẫn còn.
7. Dừng Play rồi chạy lại map: thứ tự party, HP, cấp, nâng cấp, rương đồ và trạng thái rương/quái được phục hồi.
8. Thử Encounter có nhiều prefab quái và party có một thành viên chết; người chết không hành động, có thể hồi sinh tại điểm nghỉ.


## UI phong cách Expedition (19/09/2026)

Party, rương đồ, điểm nghỉ, thuộc tính, kỹ năng, chế tạo, màn kết quả và loading hiện dùng Canvas + Unity UI + TextMeshPro. Màu sắc và bố cục nằm trong prefab, nền tối và chữ vàng ngà. Nội dung tiếng Việt; CanvasScaler dùng độ phân giải tham chiếu 1920 × 1080 với chế độ Expand để tránh cắt UI trên màn hình khác tỉ lệ.

Menu P hiển thị mô hình party bằng camera riêng/render texture; chỉ render lại khi đổi nhân vật/đội hình, không chạy gameplay trên mô hình xem trước. Nhân vật lấy từ prefab hiện tại của dự án. Q/R chuyển thành viên; ↑/↓ cũ được thay bằng hai nút đổi vị trí trái/phải dưới thẻ party.

Điểm nghỉ có trang chính và các trang riêng: thuộc tính, kỹ năng, chế tạo. Ô kỹ năng hình thoi hiển thị chi phí hoặc Đã mở; kỹ năng khóa có icon tối. Nút mở khóa bị khóa khi thiếu điểm hoặc chưa mở kỹ năng tiền đề; mọi điều kiện khoảng cách/điểm/nguyên liệu vẫn do CampaignSession kiểm tra.

Kết thúc trận sẽ hiện VICTORY hoặc DEFEAT, dừng gameplay và chờ bấm Enter/nút Tiếp tục. Phần thưởng chỉ được ghi một lần khi kết thúc trận, nút Tiếp tục không cộng thưởng lại. Kết quả gồm EXP, vật phẩm, cấp/EXP party, sát thương gây/nhận, đòn mạnh nhất, số lần parry thành công, số quái bị hạ và thời gian trận. Chạy combattest trực tiếp cũng có màn kết quả (không có phần thưởng campaign).

Mọi chuyển cảnh chiến đấu qua `LoadingScreen.Load(...)`: mapgame → Loading → combattest và kết quả → Loading → mapgame. Scene Loading được thêm vào Build Settings. Thanh tải lấy tiến độ AsyncOperation, chờ hoàn tất tải dữ liệu và hiển thị tối thiểu 1,2 giây trước khi kích hoạt scene đích. Trong bước kích hoạt cảnh lớn, Unity có thể tạm dừng vài khung hình.

Kiểm tra bổ sung trong Play Mode:

1. Mở P: thấy đúng hai model/HP; đổi vị trí, vào trận kiểm tra đúng slot.
2. Vào điểm nghỉ: thử mọi trang, chuyển thành viên Q/R, nâng/craft khi đủ và thiếu tài nguyên.
3. Thắng/thua: panel giữ nguyên cho đến Enter; không nhận input parry/skill ở màn kết quả.
4. Bấm Tiếp tục nhiều lần: chỉ chuyển cảnh một lần, thưởng chỉ cộng một lần.
5. Xem Loading ở cả hai chiều; trở về đúng vị trí hoặc checkpoint khi thua, cursor hoạt động đúng.
6. Kiểm tra giao diện ở 1920×1080, 1280×720 và màn hình ultrawide.


## Chỉnh UI bằng Canvas / Inspector

UI phiêu lưu không còn dùng OnGUI/IMGUI. Có ba prefab Canvas thật, đã gắn trực tiếp vào scene để chỉnh trước khi Play:

| Scene | Canvas trong Hierarchy | Prefab |
|---|---|---|
| mapgame | AdventureCanvas | Assets/Resources/Adventure/UI/AdventureCanvas.prefab |
| combattest | BattleResultCanvas | Assets/Resources/Adventure/UI/BattleResultCanvas.prefab |
| Loading | LoadingCanvas | Assets/Resources/Adventure/UI/LoadingCanvas.prefab |

Mở lại scene nếu Unity đang giữ phiên bản scene cũ trước khi thay đổi. Double-click prefab để mở Prefab Mode, hoặc chỉnh instance trong Hierarchy rồi Apply overrides nếu muốn áp dụng cho mọi instance.

### AdventureCanvas

- **HUD:** tên/cấp thành viên, thanh EXP, gợi ý phím và nút tương tác.
- **Menu:** Title, Subtitle, Background, Pattern, Preview/DetailPreview (RawImage dùng RenderTexture), Back và Hints.
- **Menu/Party:** Overview chứa thẻ nhân vật đứng cạnh nhau và hai nút đổi vị trí; Member là panel riêng để gán kỹ năng đã học. Không chứa rương đồ.
- **Menu/Inventory:** chọn thành viên, thông tin, Items dạng Grid và Detail hiển thị vật phẩm đang chọn.
- **Menu/Rest:** nút nghỉ/lưu, thuộc tính, kỹ năng và chế tạo.
- **Menu/Attributes, Skills, Craft:** các trang chức năng riêng.
- **Toast:** thông báo ngắn.

Trong Prefab Mode, bật trang muốn chỉnh và tắt các trang khác để nhìn rõ. Runtime tự bật đúng trang khi nhấn P/B hoặc tương tác điểm nghỉ. Không đổi tên/xóa các node đang được controller dùng để bind; có thể thêm các object trang trí khác.

### Chỉnh danh sách động

Mỗi Scroll View có `Viewport/Content/Template`. Chỉnh Template để thay đổi mẫu thẻ/dòng: Image, TextMeshPro, Button, kích thước và màu. Template hiển thị mẫu trong Edit Mode; khi chạy game, hệ thống ẩn mẫu và tạo các dòng dữ liệu từ đó. Các dòng được giữ lại và tái sử dụng, không dựng lại mỗi frame.

- VerticalLayoutGroup/GridLayoutGroup trên Content: khoảng cách, cột, chiều rộng ô.
- LayoutElement trên Template: chiều cao dòng danh sách dọc.
- RectMask2D trên Viewport: vùng cắt của danh sách cuộn.
- CanvasScaler trên Canvas: độ phân giải và cách co giãn.
- `Image.Type = Filled` và `Fill/FillAmount`: thanh HP/EXP/loading. Sprite White.png đã được gán để thanh Filled hoạt động.
- Image/RawImage trang trí và TextMeshPro tắt Raycast Target; Button và Viewport nhận tương tác.

Nút được controller gắn listener một lần, không xóa UnityEvent bạn cấu hình thêm trong Inspector. Không gắn lại chính thao tác gameplay vào OnClick của prefab nếu thao tác đó đã được controller xử lý, tránh thực hiện hai lần.

### Font và render nhân vật

AdventureCanvasRoot có Language Font và Default Font. Font Liberation Sans được tham chiếu trực tiếp để có trong bản build; runtime tạo TMP font động hỗ trợ tiếng Việt cho các text đang dùng Default Font. Nếu đổi một TextMeshPro sang font asset khác, controller giữ font tùy chỉnh của bạn. Màu/cỡ chữ, căn lề và Auto Size chỉnh trực tiếp trên từng TextMeshPro.

Preview và DetailPreview là RawImage: chỉ texture thay đổi khi chọn nhân vật; RectTransform của chúng do prefab quyết định. PartyMenuStage vẫn sử dụng camera riêng để hiển thị prefab nhân vật.

### Win/Lose và Loading

BattleResultCanvas có Title, Experience, Loot, Members, Stats, Continue và Error. Victory/Defeat dùng chung bố cục, đổi nội dung theo kết quả; màu tiêu đề thua chỉnh bằng Defeat Accent trên AdventureCanvasRoot. Game dừng khi hiện kết quả, nhưng nút Canvas vẫn dùng được. Enter và nút Continue cùng gọi một luồng chuyển cảnh đã chống bấm lặp.

LoadingCanvas có Spinner, Message, Progress, Status và Retry. Spinner quay theo thời gian không phụ thuộc TimeScale. Thanh Progress được cập nhật theo tiến độ thật của Unity.

Mỗi scene dùng EventSystem hiện có; nếu scene chưa có thì hệ thống tự tạo một EventSystem với InputSystemUIInputModule (hoặc StandaloneInputModule khi dùng legacy input). Không thêm EventSystem vào prefab để tránh tạo bản trùng trong combattest.

`Tools > Adventure > Validate Canvas UI` kiểm tra các prefab Canvas, TMP/font, sprite thanh Filled, nút bấm, Scroll View, template tái sử dụng và các instance trong scene. Các bài kiểm tra này không thay thế việc chạy Play Mode thử chuột, bàn phím và chuyển cảnh.

`Tools/CanvasUI/build_prefabs.py` là công cụ tạo layout ban đầu, mặc định từ chối ghi đè prefab đã tồn tại. Chỉnh UI qua Unity; không chạy chế độ replace-generated sau khi đã tùy chỉnh prefab.

Preview nhân vật dựng qua camera vào RenderTexture 768 × 576, lấy khung theo mesh ở tư thế đã lấy mẫu. Ảnh được lưu lại (tối đa 8 ảnh); rig, camera và đèn preview được hủy sau khi chụp. Đóng/mở menu tái sử dụng ảnh; rời map giải phóng cache. Mục đang chọn dùng lớp nền sáng và viền xanh, không thêm dấu > vào tên. Lớp sáng tắt raycast để không chặn click.

Item.icon trong CampaignConfig là hình hiển thị trong ô; nếu để trống sẽ dùng combatAction.icon khi có. Vật phẩm chưa có icon vẫn có tên và số lượng.

### Quy tắc mở khóa kỹ năng

Trong CampaignConfig, danh sách Skill Unlocks cho phép gán Skill, Cost, Initially Unlocked và Prerequisites (cần mở tất cả). Kỹ năng phải nằm trong characterSkills của prefab thành viên. Nếu không có quy tắc riêng: kỹ năng đầu tiên mở sẵn, các kỹ năng khác giá 1 điểm và không có tiền đề tự sinh. Chỉ Prerequisites được cấu hình tường minh mới chặn mở khóa. Chi tiết UI hiển thị các tiền đề còn thiếu. Nút đánh thường không bị ảnh hưởng.

Save v1 được chuyển sang v2 khi đọc, giữ các kỹ năng từng nâng thành trạng thái đã mở và hoàn lại các bậc trên 1 thành điểm kỹ năng; không đổi tên file save. Save mới vẫn theo HP, XP và party cũ, không xóa tiến trình.

### Party và 4 ô kỹ năng mang theo

P mở Overview: thẻ nhân vật có ảnh render riêng, HP, EXP, cấp và SP. Click thẻ mở Member. Chọn ô 1–4 rồi chọn kỹ năng đã học; kỹ năng đang ở ô khác sẽ hoán đổi vị trí. Nút Gỡ giữ ô trống. Esc từ Member quay lại Overview, Esc lần nữa đóng menu. Q/R đổi thành viên; ở Overview dùng Q/R chọn thành viên rồi các nút đổi vị trí. Dữ liệu mang theo lưu riêng theo thành viên, không theo vị trí trong party.

CampaignConfig.equippedSkillSlots mặc định 4. Save cũ chưa có loadout được gán tối đa 4 kỹ năng đã học lần đầu; sau đó hệ thống giữ lựa chọn và ô trống. Học kỹ năng mới không ghi đè các ô đã chọn. Trận từ mapgame chỉ dùng EquippedSkills; đánh thường không thay đổi. Học kỹ năng vẫn cần điểm nghỉ; thay kỹ năng mang theo có thể thực hiện trong Party trên map.

Trang học tự chọn kỹ năng chưa học có thể mở. Dòng Reason dưới nút cho biết thiếu điểm, tiền đề, cần điểm nghỉ hoặc đã học. Preview thẻ được tạo tuần tự và cache để hạn chế tải đồng thời nhiều model.

### Trạng thái chọn và party 4 người

Party Cards/Content dùng GridLayoutGroup tối đa 4 cột, mỗi thẻ 400 × 600 trong vùng rộng 1690. Tối đa 4 thành viên nằm cùng hàng; 1–3 thành viên được căn giữa. UI lấy thành viên thật từ dữ liệu party, không tạo nhân vật giả cho ô trống.

Thẻ party, ảnh chọn thành viên, ô kỹ năng mang theo, kỹ năng đã học, skill tree và vật phẩm đang chọn dùng nền sáng + viền xanh. Dòng thuộc tính có điểm đang phân bổ cũng được tô sáng. Không tăng kích thước nên không gây chồng thẻ hoặc cắt hình trong vùng cuộn.

statPoints thuộc từng PartyMemberProgress. Dòng điểm ghi tên nhân vật hiện tại; đổi nhân vật hủy bản nháp. Xác nhận chỉ áp dụng cho đúng nhân vật đã tạo bản nháp. Hai nhân vật cùng lên cấp có thể nhận số điểm bằng nhau nhưng không dùng chung quỹ điểm.

Vùng chọn dùng SelectionFadeGraphic: alpha mạnh nhất ở mép trên, giảm về 0 ở 55% chiều cao; phần dưới giữ nguyên, không có viền sáng bao quanh. Graphic không nhận raycast. Preview dùng đèn Point cục bộ thay Directional để không đổi main light của URP; render texture được xóa nền trước khi vẽ và chỉ hiển thị khi Ready.

### Điều hướng Main Menu bằng P

P là phím duy nhất mở menu trên map. Main Menu có Party và Inventory ở trái; preview tối đa 4 nhân vật bên phải không có Button, không nhận raycast và không kéo/cuộn. Party mở trực tiếp panel Member: chọn tên nhân vật ở danh sách bên trái, đổi 4 ô kỹ năng ở giữa, xem preview và chỉ số bên phải. Nút đổi thứ tự party nằm dưới danh sách nhân vật. Inventory giữ nguyên lưới vật phẩm và chức năng dùng thuốc.

Esc/Trở lại từ Party hoặc Inventory về Main Menu, từ Main Menu đóng menu. P đóng menu đang mở. Điểm nghỉ vẫn có luồng thuộc tính, học kỹ năng và chế tạo riêng.
