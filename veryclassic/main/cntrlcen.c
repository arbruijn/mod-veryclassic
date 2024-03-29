/*
THE COMPUTER CODE CONTAINED HEREIN IS THE SOLE PROPERTY OF PARALLAX
SOFTWARE CORPORATION ("PARALLAX").  PARALLAX, IN DISTRIBUTING THE CODE TO
END-USERS, AND SUBJECT TO ALL OF THE TERMS AND CONDITIONS HEREIN, GRANTS A
ROYALTY-FREE, PERPETUAL LICENSE TO SUCH END-USERS FOR USE BY SUCH END-USERS
IN USING, DISPLAYING,  AND CREATING DERIVATIVE WORKS THEREOF, SO LONG AS
SUCH USE, DISPLAY OR CREATION IS FOR NON-COMMERCIAL, ROYALTY OR REVENUE
FREE PURPOSES.  IN NO EVENT SHALL THE END-USER USE THE COMPUTER CODE
CONTAINED HEREIN FOR REVENUE-BEARING PURPOSES.  THE END-USER UNDERSTANDS
AND AGREES TO THE TERMS HEREIN AND ACCEPTS THE SAME BY USE OF THIS FILE.  
COPYRIGHT 1993-1998 PARALLAX SOFTWARE CORPORATION.  ALL RIGHTS RESERVED.
*/
/*
 * $Source: f:/miner/source/main/rcs/cntrlcen.c $
 * $Revision: 2.1 $
 * $Author: john $
 * $Date: 1995/03/21 14:40:25 $
 * 
 * Code for the control center
 * 
 * $Log: cntrlcen.c $
 * Revision 2.1  1995/03/21  14:40:25  john
 * Ifdef'd out the NETWORK code.
 * 
 * Revision 2.0  1995/02/27  11:31:25  john
 * New version 2.0, which has no anonymous unions, builds with
 * Watcom 10.0, and doesn't require parsing BITMAPS.TBL.
 * 
 * Revision 1.22  1995/02/11  01:56:14  mike
 * robots don't fire cheat.
 * 
 * Revision 1.21  1995/02/05  13:39:39  mike
 * fix stupid bug in control center firing timing.
 * 
 * Revision 1.20  1995/02/03  17:41:21  mike
 * fix control cen next fire time in multiplayer.
 * 
 * Revision 1.19  1995/01/29  13:46:41  mike
 * adapt to new create_small_fireball_on_object prototype.
 * 
 * Revision 1.18  1995/01/18  16:12:13  mike
 * Make control center aware of a cloaked playerr when he fires.
 * 
 * Revision 1.17  1995/01/12  12:53:44  rob
 * Trying to fix a bug with having cntrlcen in robotarchy games.
 * 
 * Revision 1.16  1994/12/11  12:37:22  mike
 * make control center smarter about firing at cloaked player, don't fire through self, though
 * it still looks that way due to prioritization problems.
 * 
 * Revision 1.15  1994/12/01  11:34:33  mike
 * fix control center shield strength in multiplayer team games.
 * 
 * Revision 1.14  1994/11/30  15:44:29  mike
 * make cntrlcen harder at higher levels.
 * 
 * Revision 1.13  1994/11/29  22:26:23  yuan
 * Fixed boss bug.
 * 
 * Revision 1.12  1994/11/27  23:12:31  matt
 * Made changes for new mprintf calling convention
 * 
 * Revision 1.11  1994/11/23  17:29:38  mike
 * deal with peculiarities going between net and regular game on boss level.
 * 
 * Revision 1.10  1994/11/18  18:27:15  rob
 * Fixed some bugs with the last version.
 * 
 * Revision 1.9  1994/11/18  17:13:59  mike
 * special case handling for level 8.
 * 
 * Revision 1.8  1994/11/15  12:45:28  mike
 * don't let cntrlcen know where a cloaked player is.
 * 
 * Revision 1.7  1994/11/08  12:18:37  mike
 * small explosions on control center.
 * 
 * Revision 1.6  1994/11/02  17:59:18  rob
 * Changed control centers so they can find people in network games.
 * Side effect of this is that control centers can find cloaked players.
 * (see in-code comments for explanation).  
 * Also added network hooks so control center shots 'sync up'.
 * 
 * Revision 1.5  1994/10/22  14:13:21  mike
 * Make control center stop firing shortly after player dies.
 * Fix bug: If play from editor and die, tries to initialize non-control center object.
 * 
 * Revision 1.4  1994/10/20  15:17:30  mike
 * Hack for control center inside boss robot.
 * 
 * Revision 1.3  1994/10/20  09:47:46  mike
 * lots stuff.
 * 
 * Revision 1.2  1994/10/17  21:35:09  matt
 * Added support for new Control Center/Main Reactor
 * 
 * Revision 1.1  1994/10/17  20:24:01  matt
 * Initial revision
 * 
 * 
 */

//#pragma off (unreferenced)
//static char rcsid[] = "$Id: cntrlcen.c 2.1 1995/03/21 14:40:25 john Exp $";
//#pragma on (unreferenced)

#include <stdlib.h>

#include "error.h"
#include "mono.h"

#include "inferno.h"
#include "cntrlcen.h"
#include "game.h"
#include "laser.h"
#include "gameseq.h"
#include "ai.h"
#include "multi.h"
#include "fuelcen.h"
#include "wall.h"
#include "object.h"
#include "robot.h"

vms_vector controlcen_gun_points[MAX_CONTROLCEN_GUNS];
vms_vector controlcen_gun_dirs[MAX_CONTROLCEN_GUNS];
int	N_controlcen_guns;
int	Control_center_been_hit;
int	Control_center_player_been_seen;
int	Control_center_next_fire_time;
int	Control_center_present;

vms_vector	Gun_pos[MAX_CONTROLCEN_GUNS], Gun_dir[MAX_CONTROLCEN_GUNS];

//	-----------------------------------------------------------------------------
//return the position & orientation of a gun on the control center object 
void calc_controlcen_gun_point(vms_vector *gun_point,vms_vector *gun_dir,object *obj,int gun_num)
{
	vms_matrix m;

	Assert(obj->type == OBJ_CNTRLCEN);
	Assert(obj->render_type==RT_POLYOBJ);

	Assert(gun_num < N_controlcen_guns);

	//instance gun position & orientation

	vm_copy_transpose_matrix(&m,&obj->orient);

	vm_vec_rotate(gun_point,&controlcen_gun_points[gun_num],&m);
	vm_vec_add2(gun_point,&obj->pos);
	vm_vec_rotate(gun_dir,&controlcen_gun_dirs[gun_num],&m);
}

//	-----------------------------------------------------------------------------
//	Look at control center guns, find best one to fire at *objp.
//	Return best gun number (one whose direction dotted with vector to player is largest).
//	If best gun has negative dot, return -1, meaning no gun is good.
int calc_best_gun(int num_guns, vms_vector *gun_pos, vms_vector *gun_dir, vms_vector *objpos)
{
	int	i;
	fix	best_dot;
	int	best_gun;

	best_dot = -F1_0*2;
	best_gun = -1;

	for (i=0; i<num_guns; i++) {
		fix			dot;
		vms_vector	gun_vec;

		vm_vec_sub(&gun_vec, objpos, &gun_pos[i]);
		vm_vec_normalize_quick(&gun_vec);
		dot = vm_vec_dot(&gun_dir[i], &gun_vec);

		if (dot > best_dot) {
			best_dot = dot;
			best_gun = i;
		}
	}

	Assert(best_gun != -1);		// Contact Mike.  This is impossible.  Or maybe you're getting an unnormalized vector somewhere.

	if (best_dot < 0)
		return -1;
	else
		return best_gun;

}

extern fix Player_time_of_death;		//	object.c

int	Dead_controlcen_object_num=-1;

//	-----------------------------------------------------------------------------
//	Called every frame.  If control center been destroyed, then actually do something.
void do_controlcen_dead_frame(void)
{
	if (!Control_center_present)
		return;

	if ((Dead_controlcen_object_num != -1) && (Fuelcen_seconds_left > 0))
		if (rand() < FrameTime*4)
			create_small_fireball_on_object(&Objects[Dead_controlcen_object_num], F1_0*3, 1);
}

//	-----------------------------------------------------------------------------
//	Called when control center gets destroyed.
//	This code is common to whether control center is implicitly imbedded in a boss,
//	or is an object of its own.
//	if objp == NULL that means the boss was the control center and don't set Dead_controlcen_object_num
void do_controlcen_destroyed_stuff(object *objp)
{
	int	i;

	// Must toggle walls whether it is a boss or control center.
	for (i=0;i<ControlCenterTriggers.num_links;i++)
		wall_toggle(&Segments[ControlCenterTriggers.seg[i]], ControlCenterTriggers.side[i]); 

	// And start the countdown stuff.
	Fuelcen_control_center_destroyed = 1;


	if (!Control_center_present)
		return;

	if (objp != NULL)
		Dead_controlcen_object_num = objp-Objects;

}

//	-----------------------------------------------------------------------------
//do whatever this thing does in a frame
void do_controlcen_frame(object *obj)
{
	int			best_gun_num;

	//	If a boss level, then Control_center_present will be 0.
	if (!Control_center_present)
		return;

#ifndef NDEBUG
	if (!Robot_firing_enabled || (Game_suspended & SUSP_ROBOTS))
		return;
#else
	if (!Robot_firing_enabled)
		return;
#endif

	if (!(Control_center_been_hit || Control_center_player_been_seen)) {
		if (!(FrameCount % 8)) {		//	Do every so often...
			vms_vector	vec_to_player;
			fix			dist_to_player;
			int			i;
			segment		*segp = &Segments[obj->segnum];

			// This is a hack.  Since the control center is not processed by
			// ai_do_frame, it doesn't know to deal with cloaked dudes.  It
			// seems to work in single-player mode because it is actually using
			// the value of Believed_player_position that was set by the last
			// person to go through ai_do_frame.  But since a no-robots game
			// never goes through ai_do_frame, I'm making it so the control
			// center can spot cloaked dudes.  

			if (Game_mode & GM_MULTI)
				Believed_player_pos = Objects[Players[Player_num].objnum].pos;

			//	Hack for special control centers which are isolated and not reachable because the
			//	real control center is inside the boss.
			for (i=0; i<MAX_SIDES_PER_SEGMENT; i++)
				if (segp->children[i] != -1)
					break;
			if (i == MAX_SIDES_PER_SEGMENT)
				return;

			vm_vec_sub(&vec_to_player, &ConsoleObject->pos, &obj->pos);
			dist_to_player = vm_vec_normalize_quick(&vec_to_player);
			if (dist_to_player < F1_0*200) {
				Control_center_player_been_seen = player_is_visible_from_object(obj, &obj->pos, 0, &vec_to_player);
				Control_center_next_fire_time = 0;
			}
		}			

		return;
	}

	if ((Control_center_next_fire_time < 0) && !(Player_is_dead && (GameTime > Player_time_of_death+F1_0*2))) {
		if (Players[Player_num].flags & PLAYER_FLAGS_CLOAKED)
			best_gun_num = calc_best_gun(N_controlcen_guns, Gun_pos, Gun_dir, &Believed_player_pos);
		else
			best_gun_num = calc_best_gun(N_controlcen_guns, Gun_pos, Gun_dir, &ConsoleObject->pos);

		if (best_gun_num != -1) {
			vms_vector	vec_to_goal;
			fix			dist_to_player;
			fix			delta_fire_time;

			if (Players[Player_num].flags & PLAYER_FLAGS_CLOAKED) {
				vm_vec_sub(&vec_to_goal, &Believed_player_pos, &Gun_pos[best_gun_num]);
				dist_to_player = vm_vec_normalize_quick(&vec_to_goal);
			} else {
				vm_vec_sub(&vec_to_goal, &ConsoleObject->pos, &Gun_pos[best_gun_num]);
				dist_to_player = vm_vec_normalize_quick(&vec_to_goal);
			}

			if (dist_to_player > F1_0*300)
			{
				Control_center_been_hit = 0;
				Control_center_player_been_seen = 0;
				return;
			}
	
			#ifdef NETWORK
			if (Game_mode & GM_MULTI)
				multi_send_controlcen_fire(&vec_to_goal, best_gun_num, obj-Objects);	
			#endif
			Laser_create_new_easy( &vec_to_goal, &Gun_pos[best_gun_num], obj-Objects, CONTROLCEN_WEAPON_NUM, 1);

			//	1/4 of time, fire another thing, not directly at player, so it might hit him if he's constantly moving.
			if (rand() < 32767/4) {
				vms_vector	randvec;

				make_random_vector(&randvec);
				vm_vec_scale_add2(&vec_to_goal, &randvec, F1_0/4);
				vm_vec_normalize_quick(&vec_to_goal);
				#ifdef NETWORK
				if (Game_mode & GM_MULTI)
					multi_send_controlcen_fire(&vec_to_goal, best_gun_num, obj-Objects);
				#endif
				Laser_create_new_easy( &vec_to_goal, &Gun_pos[best_gun_num], obj-Objects, CONTROLCEN_WEAPON_NUM, 1);
			}

			delta_fire_time = (NDL - Difficulty_level) * F1_0/4;
			if (Game_mode & GM_MULTI) // slow down rate of fire in multi player
				delta_fire_time *= 2;

			Control_center_next_fire_time = delta_fire_time;

		}
	} else
		Control_center_next_fire_time -= FrameTime;

}

//	-----------------------------------------------------------------------------
//	This must be called at the start of each level.
//	If this level contains a boss and mode != multiplayer, don't do control center stuff.  (Ghost out control center object.)
//	If this level contains a boss and mode == multiplayer, do control center stuff.
void init_controlcen_for_level(void)
{
	int		i;
	object	*objp;
	int		cntrlcen_objnum=-1, boss_objnum=-1;

	for (i=0; i<=Highest_object_index; i++) {
		objp = &Objects[i];
		if (objp->type == OBJ_CNTRLCEN) {
			if (cntrlcen_objnum != -1)
				mprintf((1, "Warning: Two or more control centers including %i and %i\n", i, cntrlcen_objnum));
			else
				cntrlcen_objnum = i;
		}

		if ((objp->type == OBJ_ROBOT) && (Robot_info[objp->id].boss_flag)) {
//		 	mprintf((0, "Found boss robot %d.\n", objp->id));
			if (boss_objnum != -1)
				mprintf((1, "Warning: Two or more bosses including %i and %i\n", i, boss_objnum));
			else
				boss_objnum = i;
		}
	}

#ifndef NDEBUG
	if (cntrlcen_objnum == -1) {
		mprintf((1, "Warning: No control center.\n"));
		return;
	}

#endif
	if ( (boss_objnum != -1) && !((Game_mode & GM_MULTI) && !(Game_mode & GM_MULTI_ROBOTS)) ) {
		if (cntrlcen_objnum != -1) {
//			mprintf((0, "Ghosting control center\n"));
			Objects[cntrlcen_objnum].type = OBJ_GHOST;
			Objects[cntrlcen_objnum].render_type = RT_NONE;
			Control_center_present = 0;
		}
	} else {
		//	Compute all gun positions.
		objp = &Objects[cntrlcen_objnum];
		for (i=0; i<N_controlcen_guns; i++)
			calc_controlcen_gun_point(&Gun_pos[i], &Gun_dir[i], objp, i);
		Control_center_present = 1;

		//	Boost control center strength at higher levels.
		if (Current_level_num >= 0)
			objp->shields = F1_0*200 + (F1_0*200/4) * Current_level_num;
		else
			objp->shields = F1_0*200 - Current_level_num*F1_0*100;
	}

	//	Say the control center has not yet been hit.
	Control_center_been_hit = 0;
	Control_center_player_been_seen = 0;
	Control_center_next_fire_time = 0;
	
	Dead_controlcen_object_num = -1;
}
