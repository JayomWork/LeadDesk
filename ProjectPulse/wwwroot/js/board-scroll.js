window.leadDeskBoardScroll = {
    connect(top, board, spacer) {
        if (!top || !board || !spacer) return;

        if (top._boardScrollTarget === board) {
            spacer.style.width = `${board.scrollWidth}px`;
            return;
        }

        if (top._boardScrollCleanup) top._boardScrollCleanup();
        let syncing = false;
        const fromTop = () => {
            if (syncing) return;
            syncing = true;
            board.scrollLeft = top.scrollLeft;
            syncing = false;
        };
        const fromBoard = () => {
            if (syncing) return;
            syncing = true;
            top.scrollLeft = board.scrollLeft;
            syncing = false;
        };
        const resize = new ResizeObserver(() => {
            spacer.style.width = `${board.scrollWidth}px`;
            top.scrollLeft = board.scrollLeft;
        });

        top.addEventListener("scroll", fromTop, { passive: true });
        board.addEventListener("scroll", fromBoard, { passive: true });
        resize.observe(board);
        for (const column of board.children) resize.observe(column);
        spacer.style.width = `${board.scrollWidth}px`;
        top.scrollLeft = board.scrollLeft;
        top._boardScrollTarget = board;
        top._boardScrollCleanup = () => {
            top.removeEventListener("scroll", fromTop);
            board.removeEventListener("scroll", fromBoard);
            resize.disconnect();
        };
    }
};
