#include "Misc/AutomationTest.h"
#include "GameSubtitlePlayer.h"

#if WITH_DEV_AUTOMATION_TESTS

// The same table is pinned in the JS (estimateDuration.test.js), Unity
// (EstimateDurationTests.cs), and Godot (tests/run_tests.gd) players. Keep all four in step.
// Non-ASCII text is written as escapes so the result does not depend on source encoding.
IMPLEMENT_SIMPLE_AUTOMATION_TEST(FGameSubtitleEstimateDurationTest,
    "GameSubtitles.EstimateDuration",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)

bool FGameSubtitleEstimateDurationTest::RunTest(const FString& Parameters)
{
    struct FCase
    {
        const TCHAR* Label;
        FString      Text;
        float        CharsPerSecond;
        float        MinSeconds;
        float        MaxSeconds;
        float        Expected;
    };

    const FString Line = TEXT("Get down, now! They have seen us."); // 33 characters

    const FCase Cases[] =
    {
        { TEXT("empty text clamps to the floor"),     TEXT(""),    14.f, 1.5f, 18.f, 1.5f },
        { TEXT("one-word bark clamps to the floor"),  TEXT("No!"), 14.f, 1.5f, 18.f, 1.5f },
        { TEXT("plain line is chars / 14"),           Line,        14.f, 1.5f, 18.f, 33.f / 14.f },
        // "Ex-tra-or-di-nary, isn't it?" with soft hyphens and a curly apostrophe
        { TEXT("soft hyphens are not counted"),
          TEXT("Ex­tra­or­di­nary, isn’t it?"),
          14.f, 1.5f, 18.f, 24.f / 14.f },
        // Japanese, 25 characters, no spaces
        { TEXT("CJK counts characters, not words"),
          TEXT("今日はとても良い天気ですね。")
          TEXT("散歩に行きましょうか。"),
          14.f, 1.5f, 18.f, 25.f / 14.f },
        // Starts with U+20BB7, outside the BMP: one character, two UTF-16 code units
        { TEXT("astral character counts once"),
          TEXT("\U00020BB7野家で牛丼を食べました。"),
          4.f, 1.5f, 18.f, 13.f / 4.f },
        { TEXT("long text clamps to the ceiling"),    FString::ChrN(300, TEXT('x')), 14.f, 1.5f, 18.f, 18.f },
        { TEXT("minSeconds override"),                TEXT("Hi"),  14.f, 0.5f, 18.f, 0.5f },
        { TEXT("maxSeconds override"),                Line,        14.f, 1.5f, 2.f,  2.f },
        { TEXT("non-positive rate falls back to 14"), Line,        0.f,  1.5f, 18.f, 33.f / 14.f },
    };

    for (const FCase& Case : Cases)
    {
        const float Actual = UGameSubtitlePlayer::EstimateDuration(
            Case.Text, Case.CharsPerSecond, Case.MinSeconds, Case.MaxSeconds);
        TestEqual(Case.Label, Actual, Case.Expected, 1e-4f);
    }

    TestEqual(TEXT("defaults match the shared rule"), UGameSubtitlePlayer::EstimateDuration(Line), 33.f / 14.f, 1e-4f);

    return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
