#include "Misc/AutomationTest.h"
#include "GameSubtitlePlayer.h"
#include "GameSubtitleTestRenderer.h"
#include "UObject/StrongObjectPtr.h"

#if WITH_DEV_AUTOMATION_TESTS

// The same cases are in the JS (SubtitlePlayer.test.js), Unity (SubtitlePlayerTests.cs), and
// Godot (tests/run_tests.gd) players. Keep all four in step. Unlike the others, this player
// always passes a character context, with bValid and bHasLineColor saying what it carries.
BEGIN_DEFINE_SPEC(FGameSubtitlePlayerSpec, "GameSubtitles.Player",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)

    const FString Line = TEXT("Get down, now! They have seen us."); // 33 characters

    TStrongObjectPtr<UGameSubtitleTestRenderer> Renderer;
    TStrongObjectPtr<UGameSubtitlePlayer>       Player;

    void Create(int32 MaxLines)
    {
        Renderer.Reset(NewObject<UGameSubtitleTestRenderer>());
        Player.Reset(NewObject<UGameSubtitlePlayer>());
        Player->Initialize(Renderer.Get(), MaxLines);
        Player->OnComplete.AddDynamic(Renderer.Get(), &UGameSubtitleTestRenderer::HandleComplete);
    }

    void ExpectLines(int32 Index, const TArray<FString>& Expected)
    {
        if (TestTrue(*FString::Printf(TEXT("page %d was rendered"), Index), Renderer->Rendered.IsValidIndex(Index)))
        {
            TestEqual(*FString::Printf(TEXT("page %d"), Index),
                FString::Join(Renderer->Rendered[Index], TEXT("|")), FString::Join(Expected, TEXT("|")));
        }
    }

END_DEFINE_SPEC(FGameSubtitlePlayerSpec)

void FGameSubtitlePlayerSpec::Define()
{
    AfterEach([this]()
    {
        Player.Reset();
        Renderer.Reset();
    });

    It("renders page 0 on Start, padded to MaxLines", [this]()
    {
        Create(2);
        Player->Start(TEXT("hello"), 4.f);
        TestEqual(TEXT("render count"), Renderer->Rendered.Num(), 1);
        ExpectLines(0, { TEXT("hello"), TEXT(" ") });
    });

    It("advances a page when its time is up", [this]()
    {
        Create(1);
        Player->Start(TEXT("aaaaa bbbbb"), 4.f);
        TestEqual(TEXT("render count before"), Renderer->Rendered.Num(), 1);
        Player->Tick(2.1f); // the first page gets 2 s; its ellipsis is not timed
        TestEqual(TEXT("render count after"), Renderer->Rendered.Num(), 2);
        ExpectLines(1, { TEXT("bbbbb") });
    });

    It("completes after the last page and keeps it visible", [this]()
    {
        Create(2);
        Player->Start(TEXT("hi"), 2.f);
        const int32 ClearedBefore = Renderer->Cleared;
        Player->Tick(2.5f);
        TestEqual(TEXT("completed"), Renderer->Completed, 1);
        TestEqual(TEXT("cleared on completion"), Renderer->Cleared, ClearedBefore);
        Player->Tick(5.f);
        TestEqual(TEXT("completed again"), Renderer->Completed, 1);
    });

    It("does not complete after Stop, and clears", [this]()
    {
        Create(2);
        Player->Start(TEXT("hi"), 5.f);
        const int32 ClearedBefore = Renderer->Cleared;
        Player->Stop();
        Player->Tick(10.f);
        TestEqual(TEXT("completed"), Renderer->Completed, 0);
        TestEqual(TEXT("cleared"), Renderer->Cleared, ClearedBefore + 1);
    });

    It("clears on Reset and can replay", [this]()
    {
        Create(2);
        Player->Start(TEXT("hello"), 2.f);
        Player->Reset();
        TestEqual(TEXT("page count after reset"), Player->GetPageCount(), 0);
        Player->Start(TEXT("hello"), 2.f);
        TestEqual(TEXT("render count"), Renderer->Rendered.Num(), 2);
    });

    It("does nothing when ticked before Start", [this]()
    {
        Create(2);
        Player->Tick(5.f);
        TestEqual(TEXT("render count"), Renderer->Rendered.Num(), 0);
    });

    It("skips to completion on a large tick", [this]()
    {
        Create(1);
        Player->Start(TEXT("aaaaaa bbbbbb cccccc"), 3.f);
        TestEqual(TEXT("page count"), Player->GetPageCount(), 3);
        Player->Tick(10.f);
        TestEqual(TEXT("completed"), Renderer->Completed, 1);
    });

    It("lays out preprocessor output", [this]()
    {
        // Entry 1 of the JS player's fixtures/processed.json.
        const FString Text = TEXT("In­ter­na­tion­al­i­za­tion is ex­traor­di­nar­i­ly dif­fi­cult.");
        Create(2);
        Player->Start(Text, 6.f);
        TestTrue(TEXT("more than one page"), Player->GetPageCount() > 1);
        for (const FString& L : Renderer->Rendered[0])
        {
            TestFalse(*FString::Printf(TEXT("raw soft hyphen in '%s'"), *L), L.Contains(TEXT("­")));
        }
    });

    It("passes the character context on every page", [this]()
    {
        Create(1);
        // "V: " takes 30, so line 0 has 100 - 10 - 30 = 60: two pages.
        Player->Start(TEXT("aaaaa bbbbb"), 4.f, TEXT("V"), true, FLinearColor(1.f, 0.f, 1.f));
        Player->Tick(2.1f);
        TestEqual(TEXT("context count"), Renderer->Contexts.Num(), 2);
        for (const FGameSubtitleCharacterContext& Ctx : Renderer->Contexts)
        {
            TestTrue(TEXT("bValid"), Ctx.bValid);
            TestEqual(TEXT("name"), Ctx.Name, FString(TEXT("V")));
            TestTrue(TEXT("bHasColor"), Ctx.bHasColor);
            TestEqual(TEXT("color"), Ctx.Color, FLinearColor(1.f, 0.f, 1.f));
            TestFalse(TEXT("bHasLineColor"), Ctx.bHasLineColor);
        }
    });

    It("passes an empty context without a name or line colour", [this]()
    {
        Create(2);
        Player->Start(TEXT("hello"), 2.f);
        TestFalse(TEXT("bValid"), Renderer->Contexts[0].bValid);
        TestFalse(TEXT("bHasLineColor"), Renderer->Contexts[0].bHasLineColor);
    });

    It("passes a line colour on its own", [this]()
    {
        Create(2);
        Player->Start(TEXT("hello"), 2.f, TEXT(""), false, FLinearColor::White, true, FLinearColor::Red);
        TestFalse(TEXT("bValid"), Renderer->Contexts[0].bValid);
        TestTrue(TEXT("bHasLineColor"), Renderer->Contexts[0].bHasLineColor);
        TestEqual(TEXT("line color"), Renderer->Contexts[0].LineColor, FLinearColor::Red);
    });

    It("measures the name prefix with the character-name font", [this]()
    {
        Create(2);
        Player->Start(TEXT("hello"), 2.f, TEXT("Rex"));
        bool bPrefixMeasured = false;
        for (const UGameSubtitleTestRenderer::FMeasured& M : Renderer->Measured)
        {
            if (M.Text == TEXT("Rex: "))
            {
                bPrefixMeasured |= M.bUseCharacterNameFont;
            }
            else
            {
                TestFalse(*FString::Printf(TEXT("'%s' measured with the character-name font"), *M.Text), M.bUseCharacterNameFont);
            }
        }
        TestTrue(TEXT("'Rex: ' measured with the character-name font"), bPrefixMeasured);
    });

    Describe("without a duration", [this]()
    {
        // Line estimates to 33 / 14, about 2.357 s.
        for (const float Duration : { 0.f, -1.f, NAN })
        {
            It(FString::Printf(TEXT("uses the estimate when the duration is %f"), Duration), [this, Duration]()
            {
                Create(2);
                Player->Start(Line, Duration);
                Player->Tick(2.3f);
                TestEqual(TEXT("completed before the estimate"), Renderer->Completed, 0);
                Player->Tick(0.1f);
                TestEqual(TEXT("completed after the estimate"), Renderer->Completed, 1);
            });
        }

        It("uses the estimate when the duration is omitted", [this]()
        {
            Create(2);
            Player->Start(Line);
            Player->Tick(2.3f);
            TestEqual(TEXT("completed before the estimate"), Renderer->Completed, 0);
            Player->Tick(0.1f);
            TestEqual(TEXT("completed after the estimate"), Renderer->Completed, 1);
        });
    });
}

#endif // WITH_DEV_AUTOMATION_TESTS
