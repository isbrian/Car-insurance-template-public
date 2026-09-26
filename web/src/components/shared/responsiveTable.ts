import type { TdHTMLAttributes } from 'react'

export function responsiveCell(label: string): TdHTMLAttributes<HTMLTableCellElement> {
  return { 'aria-label': label }
}
