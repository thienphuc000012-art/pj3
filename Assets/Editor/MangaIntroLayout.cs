using UnityEngine;

// All coordinates are measured from the top-left of the original portrait page.
// Layer rectangles place the whole source texture, including its transparent margin.
internal static class MangaIntroLayout
{
    public sealed class Chapter
    {
        public string id, title, background;
        public float width, height;
        public Panel[] panels;
    }

    public sealed class Panel
    {
        public Vector2[] polygon;
        public Layer[] layers;
        public Dialogue[] dialogue;
    }

    public sealed class Layer
    {
        public string name, path, role;
        public Rect rect;
    }

    public sealed class Dialogue
    {
        public string text;
        public Rect rect;
        public bool ellipse;
    }

    public static Chapter[] Create()
    {
        return new[]
        {
            new Chapter
            {
                id = "Chapter_01", title = "Chapter 01", width = 1122, height = 1402,
                background = Root(1) + "Chapter_01.png",
                panels = new[]
                {
                    P(new[] { V(18,18), V(1046,18), V(1046,278), V(18,423) },
                        new[] { L(1,1,"Crowd","Character",17,110,434.4f,325.8f) },
                        D("TRÊN LỤC ĐỊA\nAURAS, NHỮNG\nTÀN TÍCH CỔ XƯA\nTỪNG NGỦ YÊN\nHÀNG NGÀN NĂM...",33,31,169,134),
                        D("NGHE NÓI\nCÁC THÁNH\nVẬT ĐÃ\nTHỨC TỈNH\nRỒI!",231,152,109,134,true),
                        D("THẬT Ư?!\nVẬY LÀ\nTÀN TÍCH\nTHẦN CŨ\nSỐNG LẠI\nSAO...?",345,174,111,140,true),
                        D("...NAY ĐÃ\nTHỨC TỈNH.",892,146,138,79)),
                    P(new[] { V(18,437), V(1046,292), V(1046,773), V(18,687) },
                        new[]
                        {
                            L(1,2,"Relic merchant","Character",265,240,800,600),
                            L(1,3,"Relics on cushions","Relic",100,300,950,712.5f)
                        },
                        D("ĐÂY LÀ\nNHỮNG DI VẬT\nVỪA ĐƯỢC\nKHAI QUẬT\nTỪ KHU PHẾ\nTÍCH MỚI\nTHỨC TỈNH.",21,427,188,225,true),
                        D("MỖI MÓN\nĐỀU ẨN CHỨA\nSỨC MẠNH\nCỦA THỜI\nTHẦN CỔ...",892,313,155,193,true)),
                    P(new[] { V(18,702), V(464,738), V(389,1393), V(18,1393) },
                        new[] { L(1,4,"Relic hunter closeup","Character",-53,695,561,701) },
                        D("CUỐI\nCÙNG...\nCHÚNG\nCŨNG\nĐÃ XUẤT\nHIỆN.",20,738,116,169,true)),
                    P(new[] { V(481,739), V(1046,790), V(1046,974), V(685,1033), V(639,1393), V(409,1393) },
                        new[] { L(1,5,"Hunter approaching ruins","Character",400,930,307.2f,460.5f) },
                        D("VÀ VỚI THỢ SĂN\nDI VẬT NHƯ TA...\nĐÓ CHÍNH LÀ\nLỜI MỜI GỌI\nCỦA MỘT\nCUỘC PHIÊU LƯU\nMỚI.",490,765,146,157)),
                    P(new[] { V(687,1040), V(1045,989), V(1045,1393), V(660,1393) },
                        new[] { L(1,6,"Hand holding ancient ring","Relic",677,1040,350,437.34f) },
                        D("BỞI NƠI\nTẬN CÙNG\nNHỮNG TÀN TÍCH\nẤY... CÓ THỂ\nĐANG ẨN GIẤU\nCHIẾC NHẪN\nCỔ KHẮC\nVĂN TỰ...\nMỤC TIÊU\nCỦA TA\nLẦN NÀY!",894,1123,153,258))
                }
            },
            new Chapter
            {
                id = "Chapter_02", title = "Chapter 02", width = 1087, height = 1446,
                background = Root(2) + "Chapter_02.png",
                panels = new[]
                {
                    P(new[] { C2(14,10), C2(337,10), C2(538,476), C2(14,476) },
                        new[] { L(2,1,"Hunter climbing temple steps","Character",153,226,174.08f,260.95f) },
                        D("BƯỚC QUA\nKHU LỀU TRẠI,\nTA CUỐI CÙNG\nCŨNG ĐẶT CHÂN\nVÀO LÒNG\nDI TÍCH\nCỔ ĐẠI...",29,33,155,183)),
                    P(new[] { C2(353,10), C2(1110,10), C2(1110,476), C2(552,476) },
                        new[] { L(2,2,"Distant ring and pedestal","Relic",729,340,80,100) },
                        D("BÊN TRONG\nLÀ ĐIỆN THỜ\nTRUNG TÂM,\nNƠI LƯU GIỮ\nTHÁNH VẬT\nMÀ TA TÌM\nKIẾM.",924,34,146,187)),
                    P(new[] { C2(15,490), C2(615,490), C2(615,963), C2(15,1007) },
                        new[] { L(2,3,"Hunter disarming pressure trap","Character",146,495,392.7f,490.7f) },
                        D("LẠI LÀ\nBẪY ÁP LỰC...\nDI TÍCH NÀY\nQUẢ THẬT\nCHỈ MỞ LỐI\nCHO KẺ ĐỦ\nTRÍ TUỆ.",24,508,170,231,true),
                        D("MỘT BƯỚC\nSAI, CẢ ĐIỆN\nTHỜ SẼ\nSẬP XUỐNG.",476,510,138,175,true)),
                    P(new[] { C2(633,490), C2(1110,490), C2(1110,923), C2(632,963) },
                        new[] { L(2,4,"Floating ring and pedestal closeup","Relic",594,490,465,500) },
                        D("VÀ Ở ĐÓ...\nTHÁNH VẬT\nCỔ XƯA VẪN\nLẶNG LẼ TỎA\nRA KHÍ TỨC\nTHẦN BÍ.",930,512,141,162)),
                    P(new[] { C2(15,1023), C2(1110,938), C2(1110,1389), C2(15,1389) },
                        new[] { L(2,5,"Hunter claiming the ring","Character",150,907,796.4f,597.3f) },
                        D("DI TÍCH NÀY\nQUẢ NHIÊN\nKHÔNG DÀNH\nCHO KẺ YẾU.\nNÓ THỬ THÁCH\nCẢ SỨC MẠNH,\nTRÍ TUỆ LẪN\nÝ CHÍ...",32,1049,165,222),
                        D("CUỐI CÙNG...\nTA ĐÃ LẤY\nĐƯỢC\nTHÁNH VẬT!",883,1063,191,252,true))
                }
            },
            new Chapter
            {
                id = "Chapter_03", title = "Chapter 03", width = 1088, height = 1445,
                background = Root(3) + "Chapter_03.png",
                panels = new[]
                {
                    P(new[] { V(20,14), V(1067,14), V(1067,268), V(20,306) },
                        new[] { L(3,1,"Hunter activating ring","Character",38,-230,718.08f,897.28f) },
                        D("DI VẬT NHỎ BÉ\nVỪA ĐƯỢC KÍCH\nHOẠT... ĐÃ MỞ RA\nMỘT THẾ GIỚI ẨN\nCÁCH BIỆT VỚI\nNHÂN GIAN.",33,29,176,165),
                        D("VÀ CŨNG\nĐÁNH THỨC\nNGƯỜI GÁC\nCỔ XƯA\nCỦA NÓ!",913,29,146,151)),
                    P(new[] { V(20,319), V(404,305), V(353,538), V(20,538) },
                        new[] { L(3,2,"Ring worn on hand","Relic",102,318,289.6f,217.2f) },
                        D("TA ĐEO\nCHIẾC NHẪN\nLÊN... VÀ\nMƯỢN SỨC\nMẠNH CỦA\nCỔ VẬT...",33,326,121,167)),
                    P(new[] { V(417,304), V(1067,280), V(1067,537), V(369,537) },
                        new Layer[0],
                        D("...ĐỂ TRIỆU\nHỒI CÁNH\nCỔNG DẪN\nĐẾN THẾ\nGIỚI KHÁC!",942,295,122,146)),
                    P(new[] { V(20,555), V(1067,548), V(1067,797), V(20,806) },
                        new[] { L(3,4,"Hunter entering portal","Character",456,570,153.6f,230.25f) }),
                    P(new[] { V(20,821), V(1067,812), V(1067,1428), V(20,1428) },
                        new[] { L(3,5,"Hunter overlooking distant castle","Character",-20,844,382,573) },
                        D("VÀ PHÍA XA KIA...\nLÀ LÂU ĐÀI NƠI\nTỒN TẠI KẺ CANH GIỮ\nTHẬT SỰ CỦA\nTHẾ GIỚI NÀY!",852,828,206,149))
                }
            }
        };
    }

    private static string Root(int chapter)
    {
        return "Assets/asset/intro/Chapter_" + chapter.ToString("00") + "/";
    }

    private static Vector2 V(float x, float y) { return new Vector2(x, y); }

    // Chapter 2's clean supplied background has a different size from its reference.
    private static Vector2 C2(float x, float y) { return V(x * 1087f / 1122f, y * 1446f / 1402f); }

    private static Panel P(Vector2[] polygon, Layer[] layers, params Dialogue[] dialogue)
    {
        return new Panel { polygon = polygon, layers = layers, dialogue = dialogue };
    }

    private static Layer L(int chapter, int panel, string name, string role, float x, float y, float width, float height)
    {
        return new Layer
        {
            name = name, role = role,
            path = Root(chapter) + "Panel_" + panel.ToString("00") + ".png",
            rect = new Rect(x, y, width, height)
        };
    }

    private static Dialogue D(string text, float x, float y, float width, float height, bool ellipse = false)
    {
        return new Dialogue { text = text, rect = new Rect(x, y, width, height), ellipse = ellipse };
    }
}
