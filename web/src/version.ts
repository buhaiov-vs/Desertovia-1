import { version } from '../package.json'

/** Package metadata is the single release-number source; display 0.5.0 as 0.5. */
export const GAME_VERSION = version.endsWith('.0') ? version.slice(0, -2) : version
