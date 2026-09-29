osu-md osu-md--wiki'>Song setup window

The **song setup** window is the fourth section of the beatmap editor, which includes beatmap metadata, difficulty and design settings, as well as other miscellaneous options.

## General

![](https://i.ppy.sh/6306f1a90e51c4d0a6dca2c2bb9d57b2a0ddef3a/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f436c69656e742f426561746d61705f656469746f722f536f6e675f73657475702f696d672f53535f47656e6572616c2e6a7067)*An example of a difficulty with completed metadata*

`General` provides information which helps players not only find the beatmap, but also know a bit more about the song itself. The metadata specified here must be taken from the song's primary metadata source and, if a beatmap is going through the ranking procedure, follow the ranking criteria.

Field

Meaning

`Artist`

The band, singer, composer, or the group that performed or created the song.

`Romanised Artist`

The artist's romanised name. *Note: only editable when `Artist` contains Unicode characters.*

`Title`

The song's title.

`Romanised Title`

The song's romanised name. *Note: only editable when `Title` contains Unicode characters.*

`Beatmap Creator`

Username of the beatmap host. Authors of the guest difficulties should be specified in tags instead.

`Difficulty`

Name of the difficulty, which should reflect its contents. May indicate guest participation, be standardised, or custom.

`Source`

(Optional) The song's origin media, such as a video game or a movie.

`Tags`

Keywords for search, separated by spaces. May contain anything relevant to the beatmap or the song, such as the album name, guest mappers' names, or genre.

## Difficulty

**Notice** The game mode-specific ranking criteria contain recommended settings for every difficulty level.

![](https://i.ppy.sh/c104585e527d06e871cef009da3216232be0ab75/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f436c69656e742f426561746d61705f656469746f722f536f6e675f73657475702f696d672f53535f446966666963756c74792e6a7067)*Difficulty section. This dialogue window is different for osu!mania-specific difficulties, as shown on the right.*

`Difficulty` contains settings that affect how hard and skill-demanding a beatmap generally is for players, where higher values increase difficulty. All values listed here may be affected by certain game modifiers. These settings are commonly referred to by their abbreviation followed by their value, such as "CS 4" for a beatmap with the corresponding circle size.

The values can be adjusted with 0.1x precision by holding `Shift`, as opposed to the standard step of 1.

### HP drain rate

**Main page:** Health

HP drain rate (HP) determines the amount of health recovery or damage from accurate or mistimed hits. In osu! and osu!catch, it also affects the rate of passive health drain. Higher values provide lower health recovery with higher punishments.

### Circle size

**Main page:** Circle size

![](https://i.ppy.sh/4360690774ecb9ac90230cb0bc15eec2fbc5a24a/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f436c69656e742f426561746d61705f656469746f722f536f6e675f73657475702f696d672f436972636c655f73697a65732e706e67)*Overview of various circle sizes*

Circle size (CS) determines the size of hit objects in osu! and osu!catch, where higher values make hit objects smaller. While the editor limits CS to a range of 2 to 7, it is possible to overcome the limitation by manually editing the difficulty's `.osu` file. Circle size has no effect in osu!taiko.

For osu!mania-specific beatmaps, the circle size setting is replaced with the key count (denoted with K, such as 4K for 4 keys), which defines the number of playfield columns. Checking the `Co-op mode` checkbox will double the currently selected amount (from 5 onwards), leading to a total of 10K (5), 12K (6), 14K (7), 16K (8), and 18K (9).

### Approach rate

**Main page:** Approach rate

Approach rate (AR) indicates how long osu! and osu!catch hit objects stay on the screen from the moment they appear until the time to tap/collect. Higher values mean shorter periods of visibility and less time to react.

osu!taiko and osu!mania are not affected by approach rate changes — instead, both modes use scroll speed, which is based on the song's tempo and slider velocity.

### Overall difficulty

**Main page:** Overall difficulty

**Note:** On beatmap info pages, overall difficulty is listed as `Accuracy`.

Overall difficulty (OD) is responsible for the sizes of hit windows, which define how difficult it is to achieve high accuracy. Higher OD values mean shorter timing windows, requiring more accuracy and precision. Since accuracy is important for gaining HP, overall difficulty indirectly influences how hard it is to pass the beatmap.

Low OD values on osu! maps with high tempo may cause adjacent hit windows to overlap and enforce notelock, disabling objects until earlier hit windows pass. As a result, missing a single note will trigger a cascade failure and make the player fail the beatmap.

Additional effects of increasing OD in each game mode:

-  osu!: spinners must be spun more to fill up the gauge, up to the point of becoming nearly impossible to clear on time.
-  osu!taiko: spinners require more hits to clear.
-  osu!mania and osu!catch are not affected by OD.
## Audio

![](https://i.ppy.sh/9f80c238bdb0e02993a19704fea17de79c4570b9/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f436c69656e742f426561746d61705f656469746f722f536f6e675f73657475702f696d672f53535f417564696f2e6a7067)*The Audio section. The right is shown if the beatmap contains custom timing on volume settings.*

`Audio` makes it possible to configure hitsounds for the whole beatmap at once, if it does not have any sound customisation. Much more often, mappers prefer to have fine-grained control over the sound; as a result, they abandon the `Audio` tab in favour of customising hitsounds per beatmap section by using multiple timing sections. In this case, the upper portion of the tab is inaccessible, and clicking on the `Reset Settings` button will remove all the custom timing volume adjustments.

Control

Effect

Samplesets: `Normal/Soft/Drum`

Select between different built-in samplesets.

`Enable custom overrides`

Use custom hitsounds in place of the default ones.

`Sample Volume`

Tweak the volume of hitsounds globally.

Hitsound buttons

Play hitsounds to be used.

`Samples match playback rate`

Alter hitsounds' pitch and tempo when the beatmap speed changes (both in the editor and in-game).

## Colours

![](https://i.ppy.sh/9598ce4de3523ac434906e99892192875f7467ad/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f436c69656e742f426561746d61705f656469746f722f536f6e675f73657475702f696d672f53535f436f6c6f7572732e6a7067)*Colours tab*

`Colours` allows for configuring the combo colours. In previous versions of the game, the playfield's background colour could be adjusted here too, but this feature no longer has a purpose.

During gameplay, the colours of hit objects will cycle in the specified order by default, changing on every new combo. As a result, it is important to not only synchronize combos with the song, but also to pick combo colours that compliment the background and make the hit objects more readable. It is also possible to manually define the order of colours during mapping, which is called colourhaxing.

The combo colours only take effect in osu! and osu!catch.

Control

Action

`Combo 1..8`

Cycle through specified combo colours during the gameplay. Click the corresponding button to change its colour using the operating system's colour picker.

`Enable Custom Colours`

If unchecked, default combo colours of the active skin will be used instead.

`New Combo Colour`

Add a new colour to the set.

`Remove Combo Colour`

Remove the last colour from the set.

`Background Colour`

Change the blank playfield's colour.

## Design

![](https://i.ppy.sh/62079ef1a3d7293f200b489449aa6b075fdf7219/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f436c69656e742f426561746d61705f656469746f722f536f6e675f73657475702f696d672f53535f44657369676e2e6a7067)*Design tab*

`Design` contains various settings which affect the beatmap's overall look and feel.

Control

Action

`Enable countdown`

Activate the countdown animation before the beatmap starts.

`Countdown Speed`

Customise the countdown speed. `Half`: the countdown takes 8 full beats. `Normal`: 4 beats. `Double`: 2 beats.

`Countdown Offset`

How many beats earlier the countdown should start.

`Widescreen Support`

Remove pillarboxes from the sides of the playfield if the screen's aspect ratio is higher than `4:3`. This is typically only disabled when the beatmap or storyboard is designed with an older style in mind.

`Display storyboard in front of combo fire`

Put the storyboard before the combo fire. *Note: this setting has become obsolete with the removal of combo fire.*

`Display epilepsy warning`

Before the start of the beatmap, display an epilepsy warning in case of quick strobing in the video or storyboard.

`Letterbox during breaks`

Hide upper and lower portions of the background behind letterboxes during breaks. *Note: this setting is disallowed for osu!mania-specific beatmaps.*

`Preferred skin`

Temporarily switch to the skin with this name when the beatmap starts. If it is missing, show a warning instead, and run with the player's active skin. *Note: it is recommended to instead put the skin sprites into the beatmap's folder directly.*

## Advanced

![](https://i.ppy.sh/64271df9f56d4f47387cb4b3137a241b49c27b9a/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f436c69656e742f426561746d61705f656469746f722f536f6e675f73657475702f696d672f53535f416476616e6365642e6a7067)*Advanced tab*

### Stack leniency

**Main page:** Stack leniency

The stack leniency is an osu!-specific setting, which affects the behaviour of stacked objects. Normally, sliders and circles that are close in time will automatically stack and slightly shift if placed in the same spot. This is done to help players better see overlapping objects.

The stack leniency value controls the maximum amount of time between objects there can be until they no longer are stacked. Higher values allow objects with longer timeline gaps to stack on top of each other.

### Mode-specific

The `Allowed Modes` dropdown is used to create osu!taiko, osu!catch, or osu!mania difficulties: selecting anything except `All` will limit the playable game mode to the specified one.

The `Use special style (N+1 style) for mania` checkbox does not do anything, considering the player can set a personal preference in the options using the `osu!mania layout` button.

## Trivia

-  The `Design` tab's former name is `Storyboarding`.
-  The `Difficulty` tab used to contain an approximate summary of difficulty settings, titled `Approximate Difficulty Rating`, where more stars would imply a harder beatmap. Later, it was replaced with a label explaining the use of `Shift` for fine-tuning the values.
<!-- Background of PhotoSwipe.

It's a separate element as animating opacity is faster than rgba(). -->

<!-- Container that holds slides.

PhotoSwipe keeps only 3 of them in the DOM to save memory.

Don't modify these 3 pswp__item elements, data is added later on. -->

