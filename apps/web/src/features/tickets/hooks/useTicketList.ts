import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  // What is in the search box right now; copied into filters once typing settles.
  const [searchText, setSearchText] = useState(emptyFilters.search);
  const [state, setState] = useState<State>({ isLoading: true });

  const load = useCallback(async () => {
    setState((current) => ({ ...current, isLoading: true, error: undefined }));

    try {
      const data = await ticketsApi.getTickets(filters);
      setState({ isLoading: false, data, error: undefined });
    } catch (error) {
      const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
      setState((current) => ({ ...current, isLoading: false, error: message }));
    }
  }, [filters]);

  useEffect(() => {
    const handle = window.setTimeout(
      () =>
        setFilters((current) =>
          current.search === searchText ? current : { ...current, search: searchText, page: 1 },
        ),
      DEBOUNCE_MS,
    );

    return () => window.clearTimeout(handle);
  }, [searchText]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- fetching is what this effect is for; load sets isLoading before the request
    void load();
  }, [load]);

  const updateFilters = ({ search, ...patch }: Partial<TicketFilters>) => {
    if (search !== undefined) {
      setSearchText(search);
    }

    if (Object.keys(patch).length > 0) {
      setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));
    }
  };

  const resetFilters = () => {
    setSearchText(emptyFilters.search);
    setFilters(emptyFilters);
  };

  return {
    filters: { ...filters, search: searchText },
    updateFilters,
    resetFilters,
    reload: load,
    ...state,
  };
}
