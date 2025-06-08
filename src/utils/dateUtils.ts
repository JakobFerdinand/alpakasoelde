export function calculateAge(geburtsdatum: string | number | Date): string {
  const birth = new Date(geburtsdatum);
  const now = new Date();
  let years = now.getFullYear() - birth.getFullYear();
  let months = now.getMonth() - birth.getMonth();
  if (months < 0) {
    years--;
    months += 12;
  }
  return `${years} J ${months} M`;
}
