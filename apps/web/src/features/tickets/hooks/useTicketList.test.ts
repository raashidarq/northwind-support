import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import type { TicketFilters } from '../../../types/api';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  // DEFECT-117: only search and paging used to trigger a request.
  it.each<[string, Partial<TicketFilters>]>([
    ['status', { status: 'InProgress' }],
    ['priority', { priority: 'High' }],
    ['category', { categoryId: 3 }],
    ['customer', { customerId: 2 }],
    ['assigned agent', { assignedAgentId: 4 }],
    ['unassigned only', { unassignedOnly: true }],
    ['sort', { sortBy: 'priority', sortDirection: 'asc' }],
  ])('requests immediately when the %s filter changes', async (_name, patch) => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    getTickets.mockClear();

    await act(async () => {
      result.current.updateFilters(patch);
    });

    // No timer advanced: a dropdown change must not wait for the search debounce.
    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ ...patch, page: 1 }));
  });

  it('sends one request for a burst of keystrokes, keeping the other filters', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    await act(async () => {
      result.current.updateFilters({ status: 'InProgress' });
    });

    getTickets.mockClear();

    for (const search of ['i', 'in', 'inv']) {
      act(() => {
        result.current.updateFilters({ search });
      });
      await act(async () => {
        await vi.advanceTimersByTimeAsync(100);
      });
    }

    expect(getTickets).not.toHaveBeenCalled();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'inv', status: 'InProgress' }),
    );
  });
});
