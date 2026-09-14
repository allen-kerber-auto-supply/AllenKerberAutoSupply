export function formatSalesCallDate(dateString?: string): string {
  if (!dateString) return '—';

  const match = /^(\d{4})-(\d{2})-(\d{2})(?:[T\s](\d{2}):(\d{2}))?/.exec(dateString);
  if (!match) return '—';

  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const hours = match[4] ? Number(match[4]) : 0;
  const minutes = match[5] ? Number(match[5]) : 0;
  const hour12 = hours % 12 || 12;
  const ampm = hours < 12 ? 'AM' : 'PM';

  return `${month}/${day}/${year} ${hour12}:${minutes.toString().padStart(2, '0')} ${ampm}`;
}