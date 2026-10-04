#include "Misc/AutomationTest.h"
#include "GameSubtitleTextLayout.h"

#if WITH_DEV_AUTOMATION_TESTS

// The same cases are in the JS (TextLayout.test.js), Unity (TextLayoutTests.cs), and Godot
// (tests/run_tests.gd) players. Keep all four in step.
BEGIN_DEFINE_SPEC(FGameSubtitleTextLayoutSpec, "GameSubtitles.TextLayout",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)

    const FString Shy      = TEXT("­");
    const FString Ellipsis = TEXT("…");
    const float   W        = 100.f; // 10 monospace characters; the ellipsis takes 1, so a last line gets 90

    static float Mono(const FString& Text) { return Text.Len() * 10.f; }

    TArray<TArray<FString>> Wrap(const FString& Text, int32 MaxLines, float FirstLineIndent = 0.f)
    {
        return FGameSubtitleTextLayout::WrapAndPaginate(Text, &Mono, W, MaxLines, FirstLineIndent);
    }

    // Pages as text, such as [[hello, world!]], so a mismatch reads clearly.
    static FString PagesToString(const TArray<TArray<FString>>& Pages)
    {
        TArray<FString> Parts;
        for (const TArray<FString>& Page : Pages)
        {
            Parts.Add(TEXT("[") + FString::Join(Page, TEXT(", ")) + TEXT("]"));
        }
        return TEXT("[") + FString::Join(Parts, TEXT(", ")) + TEXT("]");
    }

    void ExpectPages(const TArray<TArray<FString>>& Actual, const TArray<TArray<FString>>& Expected)
    {
        TestEqual(TEXT("pages"), PagesToString(Actual), PagesToString(Expected));
    }

    void ExpectTimings(const TArray<TArray<FString>>& Pages, float Total, const TArray<float>& Expected)
    {
        const TArray<float> Actual = FGameSubtitleTextLayout::AllocateTimings(Pages, Total);
        if (TestEqual(TEXT("timing count"), Actual.Num(), Expected.Num()))
        {
            for (int32 i = 0; i < Expected.Num(); ++i)
            {
                TestEqual(*FString::Printf(TEXT("timing %d"), i), Actual[i], Expected[i], 1e-4f);
            }
        }
    }

END_DEFINE_SPEC(FGameSubtitleTextLayoutSpec)

void FGameSubtitleTextLayoutSpec::Define()
{
    Describe("WrapAndPaginate", [this]()
    {
        It("gives one empty page for empty or whitespace text", [this]()
        {
            ExpectPages(Wrap(TEXT(""), 2), { {} });
            ExpectPages(Wrap(TEXT("   "), 2), { {} });
        });

        It("fits a short word on one page", [this]()
        {
            ExpectPages(Wrap(TEXT("hello"), 2), { { TEXT("hello") } });
        });

        It("wraps two words that do not fit together", [this]()
        {
            ExpectPages(Wrap(TEXT("hello world!"), 2), { { TEXT("hello"), TEXT("world!") } });
        });

        It("keeps two words together when they fit", [this]()
        {
            ExpectPages(Wrap(TEXT("hi bye"), 2), { { TEXT("hi bye") } });
        });

        It("splits on any whitespace", [this]()
        {
            ExpectPages(Wrap(TEXT("hi\tbye\n"), 2), { { TEXT("hi bye") } });
        });

        It("strips soft hyphens when the word fits whole", [this]()
        {
            ExpectPages(Wrap(TEXT("inter") + Shy + TEXT("net"), 2), { { TEXT("internet") } });
        });

        It("breaks at a soft hyphen when the word overflows", [this]()
        {
            ExpectPages(Wrap(TEXT("inter") + Shy + TEXT("national"), 2), { { TEXT("inter-"), TEXT("national") } });
        });

        It("picks the longest syllable prefix that fits", [this]()
        {
            const FString Word = FString::Join(TArray<FString>{ TEXT("in"), TEXT("ter"), TEXT("na"), TEXT("tion") }, *Shy);
            ExpectPages(Wrap(Word, 2), { { TEXT("interna-"), TEXT("tion") } });
        });

        It("forces a character break for an overlong word", [this]()
        {
            ExpectPages(Wrap(TEXT("abcdefghijklmno"), 2), { { TEXT("abcdefghij"), TEXT("klmno") } });
        });

        It("wraps multiple words across lines", [this]()
        {
            ExpectPages(Wrap(TEXT("aaaa bbbb cccc dddd"), 2), { { TEXT("aaaa bbbb"), TEXT("cccc dddd") } });
        });

        It("appends an ellipsis to non-final pages", [this]()
        {
            ExpectPages(Wrap(TEXT("aaaaa bbbbb"), 1), { { TEXT("aaaaa") + Ellipsis }, { TEXT("bbbbb") } });
            const TArray<TArray<FString>> Pages = Wrap(TEXT("aaaaa bbbbb ccccc"), 1);
            TestEqual(TEXT("final page"), FString::Join(Pages.Last(), TEXT("|")), FString(TEXT("ccccc")));
        });

        It("appends an ellipsis to all non-final pages of three", [this]()
        {
            const TArray<TArray<FString>> Pages = Wrap(TEXT("aaaaaa bbbbbb cccccc"), 1);
            if (TestEqual(TEXT("page count"), Pages.Num(), 3))
            {
                TestTrue(TEXT("page 0 ends with an ellipsis"), Pages[0][0].EndsWith(Ellipsis));
                TestTrue(TEXT("page 1 ends with an ellipsis"), Pages[1][0].EndsWith(Ellipsis));
                TestFalse(TEXT("page 2 has no ellipsis"), Pages[2][0].EndsWith(Ellipsis));
            }
        });

        It("never puts a word fragment on the last line of a non-final page", [this]()
        {
            ExpectPages(Wrap(TEXT("hello inter") + Shy + TEXT("national"), 2),
                { { TEXT("hello") + Ellipsis }, { TEXT("inter-"), TEXT("national") } });
        });

        It("preserves all words across pages", [this]()
        {
            TArray<FString> Lines;
            for (const TArray<FString>& Page : Wrap(TEXT("one two three four five six seven eight nine ten"), 1))
            {
                Lines.Append(Page);
            }
            TArray<FString> Words;
            FString::Join(Lines, TEXT(" ")).Replace(*Ellipsis, TEXT("")).ParseIntoArrayWS(Words);
            TestTrue(TEXT("contains 'one'"), Words.Contains(TEXT("one")));
            TestTrue(TEXT("contains 'ten'"), Words.Contains(TEXT("ten")));
        });

        It("never shows a raw soft hyphen", [this]()
        {
            const FString Text = FString::Join(TArray<FString>{ TEXT("in"), TEXT("ter"), TEXT("na"), TEXT("tion"),
                TEXT("al"), TEXT("i"), TEXT("za"), TEXT("tion") }, *Shy) + TEXT(" is com") + Shy + TEXT("plex");
            for (const TArray<FString>& Page : Wrap(Text, 2))
            {
                for (const FString& Line : Page)
                {
                    TestFalse(*FString::Printf(TEXT("raw soft hyphen in '%s'"), *Line), Line.Contains(Shy));
                }
            }
        });

        It("rejoins a trailing fragment when the whole word fits", [this]()
        {
            // The 10-character word is split as "ccccc-" on line 0. Its tail is alone on the last
            // line, which has no ellipsis on the final page, so the whole word fits there.
            ExpectPages(Wrap(TEXT("aa ccccc") + Shy + TEXT("ccccc"), 2), { { TEXT("aa"), TEXT("cccccccccc") } });
        });

        It("narrows only line 0 for a first-line indent", [this]()
        {
            ExpectPages(Wrap(TEXT("aaaa bbbb cccc"), 2, 30.f), { { TEXT("aaaa"), TEXT("bbbb cccc") } });
        });
    });

    Describe("AllocateTimings", [this]()
    {
        It("gives equal pages equal time", [this]()
        {
            ExpectTimings({ { TEXT("abc") }, { TEXT("def") } }, 4.f, { 2.f, 2.f });
        });

        It("does not time the ellipsis", [this]()
        {
            ExpectTimings({ { TEXT("abc") + Ellipsis }, { TEXT("def") } }, 4.f, { 2.f, 2.f });
        });

        It("allocates time in proportion to character count", [this]()
        {
            ExpectTimings({ { TEXT("abc") }, { TEXT("de") } }, 5.f, { 3.f, 2.f });
        });

        It("does not count whitespace", [this]()
        {
            ExpectTimings({ { TEXT("a b") }, { TEXT("cd") } }, 4.f, { 2.f, 2.f });
        });

        It("gives a single page all the time", [this]()
        {
            ExpectTimings({ { TEXT("hello world") } }, 3.f, { 3.f });
        });

        It("counts an empty page as one character", [this]()
        {
            ExpectTimings({ { TEXT("") } }, 2.f, { 2.f });
        });
    });
}

#endif // WITH_DEV_AUTOMATION_TESTS
