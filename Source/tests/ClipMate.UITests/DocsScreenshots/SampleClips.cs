using System.Text.Json;
using System.Windows;
using ClipMate.Core.Models;
using ClipMate.UITests.Fixtures;

namespace ClipMate.UITests.DocsScreenshots;

/// <summary>
/// The made-up clips of the sample database, oldest first. The newest clip of each collection is the one ClipMate
/// shows when the collection is opened, so each collection leads with a clip worth seeing: HTML in the Inbox, rich
/// text in Recipes, an image in Work, a file list in Safe.
/// </summary>
public static class SampleClips
{
    private const string _notepad = "notepad";
    private const string _word = "WINWORD";
    private const string _excel = "EXCEL";
    private const string _outlook = "OUTLOOK";
    private const string _browser = "msedge";
    private const string _explorer = "explorer";

    /// <summary>
    /// Every sample clip, with its content in the clip's transient properties.
    /// </summary>
    public static IReadOnlyList<Clip> Create() =>
    [
        Html(SampleCollections.Recipes, At(11, 18, 42), "Easy weeknight tomato soup - Example Kitchen - Microsoft Edge",
            "https://www.example.com/recipes/tomato-soup",
            "<h2 style=\"color:#B23A2E\">Easy weeknight tomato soup</h2><p>Ready in <b>25 minutes</b>. Serves four.</p>" +
            "<ul><li>2 cans crushed tomatoes</li><li>1 onion, chopped</li><li>3 cups vegetable broth</li><li>A splash of cream</li></ul>" +
            "<p><i>Soften the onion, add the rest, and simmer for 15 minutes.</i></p>",
            """
            Easy weeknight tomato soup
            Ready in 25 minutes. Serves four.
            2 cans crushed tomatoes
            1 onion, chopped
            3 cups vegetable broth
            A splash of cream
            Soften the onion, add the rest, and simmer for 15 minutes.
            """),

        Text(SampleCollections.Safe, At(12, 8, 12), _notepad, "Important numbers - Notepad",
            """
            Emergency contacts
            Mom: 555-0121
            Neighbor (Pat): 555-0134
            Pediatrician: 555-0156
            Plumber: 555-0178
            """),

        Text(SampleCollections.Work, At(12, 9, 31), _outlook, "Untitled - Message (HTML)",
            """
            Subject: Office closed the Friday after Thanksgiving
            Hi everyone,

            A quick reminder that the office will be closed on Friday, November 27. Please send me your time sheets by Wednesday so payroll can go out on schedule.

            Have a great holiday,
            Sam
            """),

        Text(SampleCollections.Inbox, At(12, 10, 48), _notepad, "Shopping - Notepad",
            """
            Groceries for the weekend
            Eggs
            Whole milk
            Sourdough bread
            Apples (6)
            Cheddar cheese
            Coffee
            Paper towels
            """),

        Text(SampleCollections.Recipes, At(12, 12, 15), _notepad, "Breakfast ideas - Notepad",
            """
            Pancakes (makes about 8)
            1 1/2 cups flour
            1 tablespoon sugar
            1 tablespoon baking powder
            1 egg
            1 1/4 cups milk
            Mix until just combined. Cook on a hot griddle until bubbles form, then flip.
            """),

        Text(SampleCollections.Safe, At(12, 17, 40), _notepad, "Important numbers - Notepad",
            """
            Home insurance details
            Policy number: HO-000-1234
            Renews: March 15
            Agent: Jordan, 555-0163
            """),

        Text(SampleCollections.Inbox, At(13, 9, 26), _notepad, "Messages - Notepad",
            """
            Phone message from Dana, 9:20 AM
            The dentist's office called to move your cleaning to Thursday at 2:30. Call 555-0142 if that doesn't work.
            """),

        Text(SampleCollections.Inbox, At(13, 16, 10), _browser, "Party invitation - Microsoft Edge",
            """
            218 Larkspur Lane
            Anytown, USA 00000
            Saturday at 6:00 PM. Park on the street. Call 555-0117 if you get lost.
            """),

        Text(SampleCollections.Recipes, At(13, 19, 5), _notepad, "Shopping - Notepad",
            """
            Slow cooker chili - what to buy
            1 pound ground turkey
            2 cans kidney beans
            1 can diced tomatoes
            Chili powder
            Corn bread mix
            """),

        Text(SampleCollections.Inbox, At(14, 9, 2), _browser, "My account - Public Library - Microsoft Edge",
            """
            Library books due October 28
            The Lighthouse Keeper's Garden
            A Short History of Bridges
            Renew online or call 555-0189.
            """),

        Text(SampleCollections.Work, At(14, 10, 5), _notepad, "Notes - Notepad",
            """
            Client visit on Tuesday
            Conference room B is booked from 10:00 to noon.
            Order sandwiches for six by Monday.
            Visitor parking passes are at the front desk.
            """),

        Files(SampleCollections.Safe, At(14, 19, 22),
            @"C:\Users\Public\Documents\Lake trip\Cabin reservation.pdf",
            @"C:\Users\Public\Documents\Lake trip\Packing list.docx",
            @"C:\Users\Public\Pictures\Lake trip\Sunset over the dock.jpg"),

        Text(SampleCollections.Inbox, At(15, 11, 19), _outlook, "Garage sale - Message (HTML)",
            """
            Reminder: garage sale on Saturday
            Hi neighbors, we're starting at 8:00 AM. Bring your own bags, and please drop off donations by Friday evening.
            """),

        Text(SampleCollections.Work, At(15, 15, 30), _notepad, "Notes - Notepad",
            """
            Team meeting notes - October 15
            Holiday schedule goes out next week.
            Sam will order the new printer.
            Budget review moved to October 29.
            Next meeting: Thursday at 9:00 AM.
            """),

        Image(SampleCollections.Work, At(15, 16, 10), _excel, "October budget - Excel", "October budget chart"),

        Rich(SampleCollections.Recipes, At(15, 20, 20), "Family recipes - Word",
            @"{\rtf1\ansi\deff0" +
            @"{\fonttbl{\f0\froman Georgia;}{\f1\fswiss Calibri;}}" +
            @"{\colortbl;\red178\green58\blue46;\red90\green90\blue90;}" +
            @"\pard\sa120\f0\fs40\b\cf1 Apple Crumble\b0\cf0\par" +
            @"\pard\sa120\f1\fs22\i\cf2 A cozy dessert for a cool evening. Serves six.\i0\cf0\par" +
            @"\pard\sa60\f1\fs24\b Ingredients\b0\fs22\par" +
            @"\pard\li360\fi-240\sa40\bullet\tab 6 tart apples, sliced\par" +
            @"\bullet\tab 1 cup \b brown sugar\b0\par" +
            @"\bullet\tab 3/4 cup oats\par" +
            @"\bullet\tab 1/2 cup cold butter\par" +
            @"\pard\sb120\sa60\f1\fs24\b Directions\b0\fs22\par" +
            @"\pard\sa60 Heat the oven to \b 350\'b0F\b0 . Layer the apples in a baking dish, rub the rest together, and sprinkle it on top. Bake for \ul 40 minutes\ulnone .\par" +
            "}",
            """
            Apple Crumble
            A cozy dessert for a cool evening. Serves six.
            Ingredients
            6 tart apples, sliced
            1 cup brown sugar
            3/4 cup oats
            1/2 cup cold butter
            Directions
            Heat the oven to 350°F. Layer the apples in a baking dish, rub the rest together, and sprinkle it on top. Bake for 40 minutes.
            """),

        Text(SampleCollections.Inbox, At(16, 8, 55), _word, "Lake trip - Word",
            """
            Packing list for the cabin
            Sleeping bags
            Flashlights
            Bug spray
            Board games
            Rain jackets
            """),

        Html(SampleCollections.Inbox, At(16, 12, 34), "Fall market hours - Example Market - Microsoft Edge",
            "https://www.example.com/farmers-market/fall-hours",
            "<h2 style=\"color:#2E6B3C\">Fall farmers market hours</h2>" +
            "<p>Open <b>Saturdays, 8:00 AM to 1:00 PM</b>, through the end of November.</p>" +
            "<ul><li><span style=\"color:#B23A2E\">Apples</span> and fresh cider</li><li>Pumpkins and winter squash</li><li>Local honey and baked goods</li></ul>" +
            "<p><a href=\"https://www.example.com/farmers-market/map\">See the market map</a></p>",
            """
            Fall farmers market hours
            Open Saturdays, 8:00 AM to 1:00 PM, through the end of November.
            Apples and fresh cider
            Pumpkins and winter squash
            Local honey and baked goods
            See the market map
            """),
    ];

    // A fixed Central Daylight Time moment in October 2026, so that the list shows the same dates in the images
    private static DateTimeOffset At(int day, int hour, int minute) => new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(-5));

    private static Clip Text(Guid collection, DateTimeOffset capturedAt, string application, string window, string text) => new()
    {
        CollectionId = collection,
        CapturedAt = capturedAt,
        SourceApplicationName = application,
        SourceApplicationTitle = window,
        TextContent = text,
        Type = ClipType.Text,
    };

    private static Clip Rich(Guid collection, DateTimeOffset capturedAt, string window, string rtf, string text) => new()
    {
        CollectionId = collection,
        CapturedAt = capturedAt,
        SourceApplicationName = _word,
        SourceApplicationTitle = window,
        TextContent = text,
        RtfContent = rtf,
        Type = ClipType.RichText,
    };

    private static Clip Html(Guid collection, DateTimeOffset capturedAt, string window, string sourceUrl, string fragment, string text) => new()
    {
        CollectionId = collection,
        CapturedAt = capturedAt,
        SourceApplicationName = _browser,
        SourceApplicationTitle = window,
        SourceUrl = sourceUrl,
        TextContent = text,
        HtmlContent = (string)HtmlCopy.Create(fragment, text, sourceUrl).GetData(DataFormats.Html)!,
        Type = ClipType.Html,
    };

    private static Clip Image(Guid collection, DateTimeOffset capturedAt, string application, string window, string title) => new()
    {
        CollectionId = collection,
        CapturedAt = capturedAt,
        SourceApplicationName = application,
        SourceApplicationTitle = window,
        Title = title,
        CustomTitle = true,
        ImageData = SampleChart.CreatePng(),
        Type = ClipType.Image,
    };

    // With "HDROP Expansion" on, as it is by default, a file list is a Text clip listing the paths and keeping the list
    private static Clip Files(Guid collection, DateTimeOffset capturedAt, params string[] paths) => new()
    {
        CollectionId = collection,
        CapturedAt = capturedAt,
        SourceApplicationName = _explorer,
        SourceApplicationTitle = "Lake trip - File Explorer",
        Title = $"{paths.Length} files",
        TextContent = string.Join("\r\n", paths),
        FilePathsJson = JsonSerializer.Serialize(paths),
        Type = ClipType.Text,
    };
}
