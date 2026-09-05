/**
 * Envelope returned by every list endpoint, mirroring `Smartek.Common/Pagination/PagedResult.cs`.
 *
 * Note these endpoints return this object, **not** a bare array — reading `response` as `T[]`
 * silently yields nothing.
 */
export interface PagedResult<T> {
  items: T[];
  /** Rows matching the filter across all pages, not the length of `items`. */
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export const EMPTY_PAGE: PagedResult<never> = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
  totalPages: 0,
  hasPreviousPage: false,
  hasNextPage: false
};
