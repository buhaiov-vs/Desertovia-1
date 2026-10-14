/** Один префикс для API, ассетов и SignalR; корневой запуск автора остаётся рабочим. */
export const appBase = import.meta.env.BASE_URL.replace(/\/$/, '')
export const appPath = (path: string) => appBase + (path.startsWith('/') ? path : '/' + path)
