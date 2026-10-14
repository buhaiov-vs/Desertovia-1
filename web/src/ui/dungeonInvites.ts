const declinedKey = (runId: string) => `desertod.dungeon-declined.${runId}`

/** The player closed the invitation to this run: it does not pop up again. */
export const declinedDungeon = (runId: string) => localStorage.getItem(declinedKey(runId)) !== null
export const declineDungeon = (runId: string) => localStorage.setItem(declinedKey(runId), '1')
