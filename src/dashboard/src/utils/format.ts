export const formatDate = (value?: string | null, options?: Intl.DateTimeFormatOptions) => {
  if (!value) return '—';

  const date = new Date(value);
  if (Number.isNaN(date.valueOf())) return value;

  const formatOptions =
    options ?? {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit'
    };

  return date.toLocaleDateString('de-AT', formatOptions);
};

export const formatCurrency = (value?: number | string | null) => {
  if (value === null || value === undefined || Number.isNaN(Number(value))) return '—';

  return new Intl.NumberFormat('de-AT', { style: 'currency', currency: 'EUR' }).format(Number(value));
};
