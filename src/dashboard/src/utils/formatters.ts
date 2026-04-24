/**
 * Shared formatting utilities for the dashboard.
 */

/**
 * Formats a date string to German locale (dd.mm.yyyy).
 */
export const formatDate = (value: string | null | undefined): string => {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.valueOf())) return value;
  return date.toLocaleDateString('de-AT', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  });
};

/**
 * Formats a date string to a long German locale (e.g., "1. Januar 2024").
 */
export const formatDateLong = (value: string | null | undefined): string => {
  if (!value) return 'Unbekannt';
  const date = new Date(value);
  if (Number.isNaN(date.valueOf())) return 'Unbekannt';
  return date.toLocaleDateString('de-AT', {
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  });
};

/**
 * Formats a date string to ISO format for input fields (yyyy-mm-dd).
 */
export const formatDateForInput = (value: string | null | undefined): string => {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.valueOf())) return '';
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
};

/**
 * Formats a number as EUR currency.
 */
export const formatCurrency = (value: number | string | null | undefined): string => {
  if (value === null || value === undefined || Number.isNaN(Number(value))) return '—';
  return new Intl.NumberFormat('de-AT', {
    style: 'currency',
    currency: 'EUR'
  }).format(Number(value));
};

/**
 * Safely converts a value to a number, returning 0 for invalid values.
 */
export const toNumber = (value: number | string | null | undefined): number => {
  const num = Number(value);
  return Number.isFinite(num) ? num : 0;
};

/**
 * Calculates age in years from a birth date string.
 */
export const calculateAge = (dateString: string | null | undefined): string => {
  if (!dateString) return 'Unbekannt';
  const birthDate = new Date(dateString);
  if (Number.isNaN(birthDate.valueOf())) return 'Unbekannt';

  const today = new Date();
  let age = today.getFullYear() - birthDate.getFullYear();
  const monthDiff = today.getMonth() - birthDate.getMonth();

  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birthDate.getDate())) {
    age--;
  }

  if (age < 0) return 'Unbekannt';
  return `${age} Jahr${age === 1 ? '' : 'e'}`;
};

/**
 * Formats a timestamp to German locale with time zone.
 */
export const formatTimestamp = (value: string | null | undefined): string => {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.valueOf())) return value;
  return date.toLocaleString('de-AT', { timeZone: 'Europe/Vienna' });
};
