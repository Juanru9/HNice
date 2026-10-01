using FluentAssertions;
using HNice.Model.Packets;

namespace HNice.Test.Model.Packets;

public class TestsForCatalog
{
    // CATALOGINDEX captured live (header included, [2] notation).
    private const string IndexPacket = "A~HHH-1[2]root[2]HRCIHQufront_page[2]Cat\u00c3\u00a1logo[2]HHIHQDorigins_rare[2]Rare[2]HHIJRC-1[2]Campaign[2]HIIHHorigins_deepgrove[2]Deepgrove[2]HHIRAQB-1[2]Habbo Club[2]HJIHHorigins_market[2]Mercado[2]HHIHHorigins_habloons[2]Habloons[2]HHIKPC-1[2]Creative[2]HRAIHHorigins_accessotires_creative[2]Accessories[2]HHIHHorigins_creative_area[2]Area[2]HHIHHorigins_functional_creative[2]Functional[2]HHIHHorigins_creative_lodge[2]R\u00c3\u00bastico[2]HHIHHorigins_mode_creative[2]Mode[2]HHIHHorigins_creative_plants[2]Plants[2]HHIJJ-1[2]Collections[2]HPDIHHlegacy_accessories[2]Especial[2]HHIHHbc_area[2]Area[2]HHIHHlegacy_bathroom[2]Ba\u00c3\u00b1o[2]HHIIHlegacy_candy[2]Candy[2]HHIHHorigins_executive[2]Executive[2]HHIHHlegacy_flags[2]Banderas[2]HHIHHgallery[2]Galer\u00c3\u00ada[2]HHIHHlegacy_iced[2]Iced[2]HHIHSIset_mode[2]Mode[2]HHIHQIset_lodge[2]R\u00c3\u00bastico[2]HHIHHorigins_dpoly[2]Dark Mode[2]HHIHHlegacy_plants[2]Plantas[2]HHIHHlegacy_pura[2]Pura[2]HHIHHlegacy_plastic[2]Plasto[2]HHIHHlegacy_rugs[2]Alfombras[2]HHIHHorigins_windows[2]Ventanas[2]HHIQBQC-1[2]Functional[2]HQAIHHorigins_functional[2]Functional[2]HHIHHrollers[2]Rollers[2]HHIHHlegacy_oneway[2]Solo Ida[2]HHIHHteleport[2]Teleportadores[2]HHIHHlegacy_trophies[2]Trofeo[2]HHIPBSBlegacy_spaces[2]Tapizados[2]HHISBSClegacy_camera[2]Camera[2]HHISAPB-1[2]Pets[2]HJIHHlegacy_pets_purchase[2]Mascotas[2]HHIHHlegacy_pet_accessories[2]Accesorios (Mascotas)[2]HHIPAPA-1[2]Trax[2]HJIHHlegacy_trax[2]Trax[2]HHIHHlegacy_jukebox[2]Jukebox[2]HHIHHorigins_jars[2]Dye Jars[2]HHIHRBlegacy_deals[2]Ofertas[2]HHIPBRDlegacy_presents[2]Regalos[2]HH";

    [Fact]
    public void ShouldParseTheCapturedCatalogueTree()
    {
        CatalogIndex.TryParse(IndexPacket[2..].Replace("[2]", ""), out var pages).Should().BeTrue();

        pages.Should().HaveCount(41);
        pages.Should().Contain(new CatalogPageRef("legacy_plastic", "Plasto", "Collections"));
        pages.Should().Contain(new CatalogPageRef("origins_rare", "Rare", ""));
        pages.Should().Contain(p => p.PageName == "origins_market" && p.Category == "Habbo Club");
        pages.Should().NotContain(p => p.PageName == "-1");
    }

    [Fact]
    public void PageRequestShouldMatchWhatTheClientSends()
    {
        // Captured: → GET_CATALOG_PAGE Af@Jproduction@Jfront_page@Ben
        CatalogIndex.PageRequest("front_page").Should().Be("Af@Jproduction@Jfront_page@Ben");
    }

    // CATALOGPAGE lines captured live (legacy_plastic, legacy_spaces, origins_functional).
    private static readonly string PageBody = string.Join((char)13,
        "i:legacy_plastic", "n:legacy_plastic", "", "l:ctlg_plasto",
        "p:Silla\tGuay y de plÃ¡stico\t-1\u0002\t3\tfalse\ts\tchair_plasto*9\t0\t1,1\tA1 E9P\t#ffffff,#533e10,#ffffff,#533e10\t104",
        "p:Tapizado\tSÃ³lo en las paredes\t-1\u0002\t2\tfalse\ti\twallpaper 1\t\t\twallpaper 1\t#ffffff\t646",
        "p:Conjunto\tIncluye una palanca\t-1\u0002\t5\t\td\t\t\t\ttoby_lodge_lever_gate_combo\t\t22498\t2\ttoby_lodge_gate_lever\t1\t#ffffff\ttoby_lodge_lever\t1\t#ffffff",
        "p:Mesa\tGrande\t-1\u0002\t4\tfalse\ts\ttable_plasto_bigsquare*9\t2\t2,2\tA1 X\t#ffffff,#533e10\t120");

    [Fact]
    public void ShouldParseCatalogueItems()
    {
        var items = CatalogPage.Items(PageBody, new CatalogPageRef("legacy_plastic", "Plasto", "Collections"));

        items.Should().HaveCount(3, "deals are skipped");
        var chair = items[0];
        chair.Sprite.Should().Be("chair_plasto*9");
        chair.Name.Should().Be("Silla");
        chair.Description.Should().Be("Guay y de plástico");
        chair.Price.Should().Be(3);
        chair.IsWallItem.Should().BeFalse();
        chair.Colors.Should().Be("#ffffff,#533e10,#ffffff,#533e10");
        chair.Swatches.Should().Equal("#ffffff", "#533e10");
        chair.Category.Should().Be("Collections");

        items[1].IsWallItem.Should().BeTrue();
        items[1].SizeText.Should().Be("wall");

        var table = items[2];
        (table.Width, table.Length, table.IsStateful).Should().Be((2, 2, true));
    }

    [Fact]
    public void ActiveObjectShouldCarryTheFurniSize()
    {
        var packet = ClientPacketBuilder.ActiveObject("9", 1, "table_plasto_bigsquare*9", 3, 4, 2, "#ffffff", width: 2, length: 2);
        packet.Should().Contain("table_plasto_bigsquare*9\u0002" + "KPA" + "JJ" + "J0.0");
    }

    [Fact]
    public void ActiveObjectShouldEndLikeTheLiveFurniSoItNeverExpires()
    {
        // Live: …md_limukaappi_liq[2]PARAIIJ0.0[2][2][2]HHHFALSE[2]HMM — without "HMM" the client shows "Expira en 0 minutos".
        ClientPacketBuilder.ActiveObject("233786", 1, "md_limukaappi", 4, 6, 2, state: "FALSE")
            .Should().EndWith("[2][2][2]HHHFALSE[2]HMM".Replace("[2]", "\u0002"));
    }

    [Fact]
    public void ShouldReadThePageId()
    {
        CatalogPage.PageId(string.Join((char)13, "i:front_page", "n:front_page", "l:ctlg_frontpage2")).Should().Be("front_page");
        CatalogPage.PageId("not a page").Should().BeNull();
    }
}
