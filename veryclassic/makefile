CFLAGS += -I main -I includes -I arch -I cfile -g -Wall -I bios -I 2d -I 3d -I fix -I vecmat -I misc -D _far=  -fmax-errors=5 -Werror -I mem -D far= -funsigned-char
CFLAGS += -I iff -I pslib -I texmap
CFLAGS += -DSHAREWARE
#CFLAGS += -Og -g
CFLAGS += -O2
CFLAGS += -posix
CFLAGS += -Wno-error=stringop-truncation -fwrapv -fno-strict-aliasing

SRC = veryclassic.c mycfile.c 2d/pcx.c 2d/gr.c 2d/canvas.c 2d/palette.c 2d/bitmap.c 2d/bitblt.c 2d/pixel.c 2d/gpixel.c 2d/rect.c \
	2d/line.c \
	2d/scanline.c 2d/rle.c misc/error.c main/titles.c main/text.c 2d/font.c wintime.c main/menu.c \
	main/config.c main/newmenu.c main/kconfig.c main/gamefont.c main/gameseq.c main/bm.c main/piggy.c \
	main/hash.c main/cntrlcen.c main/fuelcen.c main/gauges.c main/object.c main/ai.c main/wall.c \
	main/gameseg.c main/laser.c main/physics.c main/collide.c main/fireball.c main/powerup.c \
	main/weapon.c main/game.c main/slew.c main/aipath.c main/lighting.c main/render.c \
	main/automap.c vecmat/vecmat.c main/fvi.c main/hostage.c main/newdemo.c main/switch.c \
	main/endlevel.c iff/iff.c main/terrain.c main/gamemine.c main/hud.c main/mglobal.c \
	texmap/ntmap.c main/vclip.c main/effects.c main/robot.c main/texmerge.c main/polyobj.c \
	main/inferno.c main/args.c main/scores.c main/credits.c 2d/disc.c 2d/scale.c \
	main/state.c main/mission.c main/songs.c main/playsave.c main/gamesave.c \
	main/morph.c main/paging.c fix/fix.c 2d/ibitblt.c texmap/tmapflat.c fix/tables.c \
	main/controls.c texmap/scanline.c main/bmread.c main/joydefs.c \
	bios/ukey.c bios/key.c unadpcm.c hmpfile.c hmpmidi.c digipos.c

SRC += 3d/clipper.c 3d/draw.c 3d/globvars.c 3d/horizon.c 3d/instance.c \
	3d/interp.c 3d/matrix.c 3d/points.c 3d/rod.c 3d/setup.c

OBJ = $(patsubst %.c,%.o,$(SRC))

all: veryclassic.dll test.exe

veryclassic.dll: $(OBJ) descent.hog.bin.o descent.pig.bin.o
	$(CC) -shared -o $@ $^ -lwinmm

test.exe: test.o veryclassic.dll
	$(CC) -g -o $@ $^ veryclassic.dll

clean:
	rm -f *.o $(OBJ) test test.exe veryclassic.dll *~

%.bin.o: %
	ld -r -b binary -o $@ $^
