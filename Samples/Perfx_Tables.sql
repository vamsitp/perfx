-- DROP TABLE [dbo].[Perfx_Results]
-- DROP TABLE [dbo].[Perfx_Stats]

CREATE TABLE [dbo].[Perfx_Stats] (
    [id]            BIGINT  IDENTITY (1, 1) NOT NULL,
    [run_by]        NVARCHAR (200)          NULL,
    [url]           NVARCHAR (2048)         NOT NULL,
    [dur_min_s]     FLOAT (53)              NULL,
    [dur_max_s]     FLOAT (53)              NULL,
    [dur_mean_s]    FLOAT (53)              NULL,
    [dur_median_s]  FLOAT (53)              NULL,
    [dur_std_dev_s] FLOAT (53)              NULL,
    [dur_90_perc_s] FLOAT (53)              NULL,
    [dur_95_perc_s] FLOAT (53)              NULL,
    [dur_99_perc_s] FLOAT (53)              NULL,
    [size_min_kb]   FLOAT (53)              NULL,
    [size_max_kb]   FLOAT (53)              NULL,
    [ok_200]        FLOAT (53)              NULL,
    [other_xxx]     FLOAT (53)              NULL,
    [timestamp]     DATETIME2               DEFAULT CURRENT_TIMESTAMP NOT NULL,
    PRIMARY KEY CLUSTERED ([id] ASC)
);

CREATE TABLE [dbo].[Perfx_Results]
(
	[id]            REAL                    NOT NULL,
    [run_Id]        BIGINT                  NOT NULL,
    [url]           NVARCHAR (2048)         NULL,
    [query]         NVARCHAR (2048)         NULL,
    [result]        NVARCHAR (100)          NULL,
    [size_b]        BIGINT                  NULL,
    [size_str]      NVARCHAR (200)          NULL,
    [local_ms]      FLOAT                   NULL,
    [ai_ms]         FLOAT                   NULL,
    [local_s]       FLOAT                   NULL,
    [ai_s]          FLOAT                   NULL,
    [exp_sla_s]     FLOAT                   NULL,
    [op_Id]         NVARCHAR (100)          NULL,
    [ai_op_Id]      NVARCHAR (100)          NULL,
    [timestamp]     DATETIME2               DEFAULT CURRENT_TIMESTAMP NOT NULL,
    PRIMARY KEY CLUSTERED ([run_Id], [id]),
    CONSTRAINT [FK_Perfx_Results_Perfx_Stats] FOREIGN KEY ([run_Id]) REFERENCES [Perfx_Stats]([id])
)
