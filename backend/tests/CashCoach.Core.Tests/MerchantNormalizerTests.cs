using CashCoach.Core.Domain;
using CashCoach.Core.Services;
using FluentAssertions;

namespace CashCoach.Core.Tests;

public class MerchantNormalizerTests
{
    [Theory]
    [InlineData("ZABKA Z5521 K.1 KRAKOW", "ZABKA")]
    [InlineData("Żabka Z1234 Warszawa", "ZABKA")]
    [InlineData("BLIK 2938471 PYSZNE.PL", "PYSZNE")]
    [InlineData("BOLT.EU/O/2410", "BOLT")]
    [InlineData("PAYU*ALLEGRO", "ALLEGRO")]
    [InlineData("PAYU*ALLEGRO 94277261", "ALLEGRO")]
    [InlineData("KLARNA*ZALANDO RATA 2/4", "ZALANDO")]
    [InlineData("BIEDRONKA 4521 KRAKOW", "BIEDRONKA")]
    [InlineData("LIDL POLSKA 0123 POZNAN", "LIDL")]
    [InlineData("UBER *TRIP HELP.UBER.COM", "UBER TRIP")]
    [InlineData("GOOGLE *YOUTUBE MUSIC G.CO/HELPPAY#", "YOUTUBE MUSIC")]
    [InlineData("APPLE.COM/BILL ICLOUD", "ICLOUD")]
    [InlineData("NETFLIX.COM AMSTERDAM", "NETFLIX")]
    [InlineData("SPOTIFY P2F8A9C1D3 STOCKHOLM", "SPOTIFY")]
    [InlineData("PAYPRO*MEDIA EXPERT 123456", "MEDIA EXPERT")]
    [InlineData("SUMUP *KAWIARNIA BLOK", "KAWIARNIA BLOK")]
    [InlineData("PAYPO*MODIVO RATA 1/3", "MODIVO")]
    [InlineData("PRZELEW PRZYCHODZACY WYNAGRODZENIE ZA LIPIEC ACME SP Z O O", "WYNAGRODZENIE ACME")]
    [InlineData("PRZELEW CZYNSZ POKOJ", "CZYNSZ POKOJ")]
    [InlineData("ROSSMANN 123 ŁÓDŹ 12.08.2026", "ROSSMANN")]
    [InlineData("MCDONALDS 1234 WROCLAW 2026-08-14", "MCDONALDS")]
    [InlineData("ZAKUP PRZY UZYCIU KARTY BIEDRONKA 1234", "BIEDRONKA")]
    [InlineData("WOLT.COM/676402 KRAKOW", "WOLT")]
    [InlineData("BLIK PRZELEW NA TELEFON", "TELEFON")]
    [InlineData("OPENAI *CHATGPT SUBSCR", "OPENAI CHATGPT")]
    [InlineData("12345 67890", MerchantNormalizer.UnknownMerchantKey)]
    public void Extracts_the_merchant_key(string raw, string expectedKey)
    {
        MerchantNormalizer.Normalize(raw).MerchantKey.Should().Be(expectedKey);
    }

    [Theory]
    [InlineData("BLIK 2938471 PYSZNE.PL", Channel.Blik)]
    [InlineData("BLIK PRZELEW NA TELEFON", Channel.Blik)]
    [InlineData("PRZELEW CZYNSZ POKOJ", Channel.Transfer)]
    [InlineData("ZABKA Z5521 K.1 KRAKOW", Channel.Card)]
    public void Detects_the_channel(string raw, Channel expected)
    {
        MerchantNormalizer.Normalize(raw).Channel.Should().Be(expected);
    }

    [Theory]
    [InlineData("KLARNA*ZALANDO RATA 2/4", "Klarna", 2, 4)]
    [InlineData("PAYPO*MODIVO RATA 1/3", "PayPo", 1, 3)]
    [InlineData("TWISTO*MEDIA EXPERT RATA 1/6", "Twisto", 1, 6)]
    [InlineData("MEDIA MARKT RATA 3/10", null, 3, 10)]
    public void Flags_bnpl_instalments(string raw, string? provider, int number, int count)
    {
        var normalized = MerchantNormalizer.Normalize(raw);

        normalized.IsBnpl.Should().BeTrue();
        normalized.BnplProvider.Should().Be(provider);
        normalized.InstalmentNumber.Should().Be(number);
        normalized.InstalmentCount.Should().Be(count);
    }

    [Fact]
    public void Flags_a_bnpl_provider_without_an_instalment_marker()
    {
        var normalized = MerchantNormalizer.Normalize("KLARNA*ZALANDO");

        normalized.IsBnpl.Should().BeTrue();
        normalized.InstalmentCount.Should().BeNull();
    }

    [Theory]
    [InlineData("BOLT.EU/O/2410")]
    [InlineData("ROSSMANN 123 KRAKOW 12/08/2026")]
    [InlineData("24/7 FITNESS")]
    public void Does_not_flag_ordinary_payments_as_bnpl(string raw)
    {
        MerchantNormalizer.Normalize(raw).IsBnpl.Should().BeFalse();
    }
}
