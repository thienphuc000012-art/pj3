# Đấu trường theo vị trí spawn

## Cấu trúc của project

`mapgame` dùng **MapChunkStreamer** với 89 scene trong **MapChunksRefined**. Danh mục đã lưu đường dẫn scene và Bounds theo world space. Scene `m` có Unity Terrain và hiện chưa có danh mục MapChunkStreamer. Enemy trên map là một **WorldInteraction** loại **Encounter**. **EnemyEncounterBehaviour** di chuyển object đó và dùng một model con để trình diễn. Các **BattleUnit** trong `combattest` là các instance riêng, do **CampaignSession.PrepareBattle** tạo từ prefab và dữ liệu party.

**CampaignSession** chặn input và patrol trong đoạn chuyển cảnh. Sau khi lưu vị trí quay về và danh sách chunk, game chuyển sang scene Loading để giải phóng map cũ. Loading tải các chunk đã chọn và scene combat theo chế độ Additive. Khi trận kết thúc, CampaignSession dùng ID của encounter để ghi kết quả. Luồng quay về hiện có tải lại map và các chunk quanh vị trí quay về.

## Mốc 1 và 2. Spawn Anchor và Patrol

```text
mapgame
├─ MapGame Systems                 MapGameSetup + MapChunkStreamer
└─ Enemy Encounter                 WorldInteraction + EnemyEncounterBehaviour
   ├─ EnemyCombatOrigin            Cùng GameObject với WorldInteraction
   └─ Enemy Visuals                Animator / model
```

Với encounter đặt sẵn, **EnemyCombatOrigin** lưu pose khi encounter được kích hoạt. Patrol lấy pose đó trước khi Warp agent hoặc nâng enemy lên độ cao bay. Tắt rồi bật lại cùng một encounter giữ nguyên spawn đã lưu.

Với spawner hoặc object pool, đặt object ở vị trí mới, gọi **SetSpawnPoint**, rồi mới kích hoạt object hoặc bật patrol.

```csharp
// Pooled encounter đang inactive và đã có EnemyCombatOrigin.
pooledEncounter.transform.SetPositionAndRotation(position, rotation);
pooledEncounter.GetComponent<EnemyCombatOrigin>().SetSpawnPoint(position, rotation);
pooledEncounter.SetActive(true);
```

**CaptureSpawnPoint()** chỉ ghi khi chưa có spawn. **ResetSpawnPoint()** xóa pose của lượt spawn cũ. Nếu đã reset, phải đặt lại pose trước khi chạy patrol. **Spawn Position** dùng cho scanner. **Current Position** vẫn là vị trí thật để kiểm tra phạm vi tương tác và va chạm.

## Mốc 3. Scanner và Inspector

Trong Hierarchy, chọn **Enemy Encounter → World Interaction → Combat Arena Scan**.

| Ô | Ý nghĩa |
|---|---|
| **Scan Radius** | `0` tự chọn 18 m cho một enemy, 23 m cho nhóm và 35 m nếu có Boss. |
| **Edge Padding** | Thêm 4 m mặc định để thu thêm chunk quanh mép. |
| **Debug Scan Area** | Vẽ vòng vàng tại Spawn Position khi chọn encounter. Vòng mờ phía ngoài biểu thị phần padding. |

Scanner dùng Bounds ba chiều của chunk trong danh mục, kể cả chunk chưa load. Tâm chunk có thể nằm ngoài vòng nếu Bounds vẫn giao vùng quét. Danh sách đường dẫn không trùng. Nếu thiếu mốc spawn, thiếu streamer hoặc không có chunk phù hợp, scanner trả lỗi. Builder lọc các root gameplay sau khi tải chunk vì danh mục chỉ lưu Bounds và đường dẫn.

## Kiểm tra trong Unity

1. Trong **Edit Mode**, chọn **Tools → Adventure → Combat → Validate Spawn and Scan**. Có thể bấm nút cùng tên trong Inspector của Enemy Encounter. Console phải hiện `[Combat Arena] PASS`. Bộ kiểm tra dùng encounter và danh mục tạm, không sửa scene.
2. Trong **Play Mode**, chọn một encounter còn sống trong `mapgame`, bật Gizmos và chọn **Tools → Adventure → Combat → Inspect Selected Encounter**. Console ghi **Spawn**, **Current**, bán kính và từng đường dẫn chunk.
3. Với enemy đứng yên, **Spawn** trùng điểm đặt trên map. Với enemy đi tuần gần rồi xa 15–20 m, **Current** phải đổi còn **Spawn** và danh sách chunk giữ nguyên. Có thể đặt **Patrol Radius** và **Leash Radius** lớn hơn 20 m để tái hiện bước này.
4. Chọn lần lượt hai encounter. Mỗi cái phải in mốc spawn riêng. Bộ kiểm tra Edit Mode cũng thử trường hợp pool đưa encounter cũ đến một mốc mới.
5. Vào trận từ `mapgame`. Sau Loading, xem **combattest → CombatArenaRoot** và log `[Combat Arena] Spawn …`. Thử quay lại qua thắng, thua và chạy trốn. Xem Hierarchy để xác nhận không có bản sao của chunk/Player, kiểm tra đối tượng đã hạ vẫn bị đánh dấu và đối chiếu vị trí quay lại với save/checkpoint.

## Dựng arena và tải scene

Trong **BattleEncounterSetup → Combat Arena → Arena Spawn**, chỉnh khoảng cách hai đội, khoảng cách giữa slot, độ cao raycast và kích thước khoảng trống dành cho Boss. Builder thử các hướng quanh spawn, kiểm tra mặt đất và khoảng trống trước khi giao slot cho Combat Manager. Khi bật **Players Walk In**, arena ưu tiên bố trí cho mọi Player đi đủ **Player Entry Distance** và lấy cao độ mặt đất dọc lối đi. Nếu lối vào bị chắn, từng Player đi đoạn an toàn còn lại hoặc bắt đầu ở slot nếu ngay phía sau đã bị chắn. Những slot dự phòng không có chỗ đứng an toàn sẽ không được dùng để triệu hồi.

Các chunk hiện có là scene Unity đã chia sẵn. Builder dùng **Move Existing Chunks** với root môi trường để giữ world transform, mesh, material và collider, đồng thời giữ các scene chunk additive cho đến khi thoát trận. Root chứa gameplay như AI, BattleUnit, Player, UI hoặc camera bị loại. Nếu thiếu chunk hoặc thiếu chỗ đứng, Loading hiển thị lỗi và cho quay lại map. Combat Manager bắt đầu sau khi arena và slot đã sẵn sàng.

`m` hiện có Unity Terrain nhưng chưa có danh mục MapChunkStreamer. Map chưa có catalogue vẫn dùng arena cũ. Các map cũ được giữ ẩn trong dữ liệu preset để hỗ trợ chế độ đó, còn encounter đã dựng từ chunk sẽ tắt các map preset.
