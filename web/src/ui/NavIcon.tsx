/** One stroke weight and viewbox across the game navigation. */
export function NavIcon({ name }: { name: string }) {
  const paths: Record<string, string> = {
    city: 'M3 21V9l5-4 5 4v12M13 21V3h8v18M1 21h22M7 21v-6h3M16 7h2M16 11h2M16 15h2',
    person: 'M16 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0ZM4 22v-3a8 8 0 0 1 16 0v3',
    tasks: 'M9 4h12v17H3V4h3M8 2h8v5H8ZM7 12h10M7 17h10',
    build: 'm3 21 12-12M12 3l9 9-3 3-9-9ZM3 16l5 5',
    science: 'M4 3h6v18H4ZM14 3h6v18h-6ZM5 7h4M15 7h4M5 17h4M15 17h4',
    settings: 'M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8ZM12 2v3M12 19v3M2 12h3M19 12h3M5 5l2 2M17 17l2 2M5 19l2-2M17 7l2-2',
    storage: 'M3 8 12 3l9 5v13H3ZM3 8h18M8 21v-8h8v8',
  }
  return <svg className="nav-icon" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"><path d={paths[name] ?? paths.city}/></svg>
}
