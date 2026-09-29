osu-md osu-md--wiki'>osu! file formats

## List of file formats

File format

MIME type

Usage

.osz

`x-osu-beatmap-archive`

osu! beatmap archive

.osk

`x-osu-skin-archive`

osu! skin archive

.osu

`x-osu-beatmap`

osu! difficulty

.osb

`x-osu-storyboard`

osu! storyboard

.osr

`x-osu-replay`

osu! replay

## Creating .osz and .osk files

`.osz` and `.osk` files are archives containing beatmaps and skins respectively. Their contents are automatically extracted by osu! when opening them:

-  `.osz` files are extracted into the `Songs` directory
-  `.osk` files are extracted into the `Skins` directory
Knowing how to make a skin or beatmap archive recognised by osu! will make your works more accessible to other users.

### Using an archiver

1. Install a file archiver which may produce `.zip` files, such as 7-Zip or WinRAR.
1. Put all of the files you want to archive in a separate folder.
1. Right-click the folder and select `Add to archive...` (alternatively, do that from the file archiver itself).
1. Select the ZIP archive format.
1. In the archive's file name, change the `.zip` extension to `.osz` (`.osk` if you're archiving a skin).
To test the archive, make a copy of the file and open it via osu!.

### Using osu!

To produce a beatmap archive:

-  Open a beatmap via the editor.
-  From the top menu, select `File` > `Export package...`.
-  The `.osz` archive will be placed in the `Exports` folder inside the osu! folder.
To produce a skin archive:

-  Make sure your skin has everything you want to export. You can check by clicking the `Open Skin Folder` button in the osu! settings.
-  In the settings, click on `Select Skin`.
-  Choose the skin you want to export and click `Export as .osk`.
-  The `.osk` archive will be placed in the `Exports` folder in the osu! installation folder.
<!-- Background of PhotoSwipe.

It's a separate element as animating opacity is faster than rgba(). -->

<!-- Container that holds slides.

PhotoSwipe keeps only 3 of them in the DOM to save memory.

Don't modify these 3 pswp__item elements, data is added later on. -->

