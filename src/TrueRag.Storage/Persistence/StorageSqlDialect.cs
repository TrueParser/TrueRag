namespace TrueRag.Storage.Persistence;

internal abstract class StorageSqlDialect
{
    protected StorageSqlDialect(DatabaseEngine engine)
    {
        Engine = engine;
    }

    public DatabaseEngine Engine { get; }

    public static StorageSqlDialect Create(DatabaseEngine engine) =>
        engine switch
        {
            DatabaseEngine.CrateDb => new CrateDbStorageSqlDialect(),
            DatabaseEngine.PostgreSql => new PostgreSqlStorageSqlDialect(),
            _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unknown database engine.")
        };

    public abstract string BuildVectorQuerySql();

    public abstract string BuildTextQuerySql();

    public abstract string BuildHybridQuerySql();

    public abstract string BuildUpsertSql();

    protected static string CommonPredicateSql =>
        "tenant_id = @tenant_id AND app_id = @app_id AND collection_id = @collection_id AND (@acl_groups IS NULL OR allowed_document_groups && @acl_groups) AND (@required_fidelity IS NULL OR fidelity_level = @required_fidelity)";
}

internal sealed class CrateDbStorageSqlDialect : StorageSqlDialect
{
    public CrateDbStorageSqlDialect()
        : base(DatabaseEngine.CrateDb)
    {
    }

    public override string BuildVectorQuerySql() =>
        $$"""
          SELECT id, document_id, node_type, text, _score, fidelity_level, page, x, y, w, h, logical_path, document_group_id, version_number, referenced_node_ids
          FROM nodes
          WHERE {{CommonPredicateSql}}
            AND knn_match(vector, @query_vector, @top_k)
          ORDER BY _score DESC
          LIMIT @top_k;
          """;

    public override string BuildTextQuerySql() =>
        $$"""
          SELECT id, document_id, node_type, text, _score, fidelity_level, page, x, y, w, h, logical_path, document_group_id, version_number, referenced_node_ids
          FROM nodes
          WHERE {{CommonPredicateSql}}
            AND MATCH(text, @query_text)
          ORDER BY _score DESC
          LIMIT @top_k;
          """;

    public override string BuildHybridQuerySql() =>
        $$"""
          WITH vector_hits AS (
              SELECT id, ROW_NUMBER() OVER (ORDER BY _score DESC) AS v_rank
              FROM nodes
              WHERE {{CommonPredicateSql}}
                AND knn_match(vector, @query_vector, @top_k)
              LIMIT @top_k
          ),
          text_hits AS (
              SELECT id, ROW_NUMBER() OVER (ORDER BY _score DESC) AS t_rank
              FROM nodes
              WHERE {{CommonPredicateSql}}
                AND MATCH(text, @query_text)
              LIMIT @top_k
          ),
          fused AS (
              SELECT id, SUM(rrf_score) AS score
              FROM (
                  SELECT id, (1.0 / (50 + v_rank)) AS rrf_score FROM vector_hits
                  UNION ALL
                  SELECT id, (1.0 / (50 + t_rank)) AS rrf_score FROM text_hits
              ) ranked
              GROUP BY id
          )
          SELECT n.id, n.document_id, n.node_type, n.text, f.score AS _score, n.fidelity_level, n.page, n.x, n.y, n.w, n.h, n.logical_path, n.document_group_id, n.version_number, n.referenced_node_ids
          FROM fused f
          INNER JOIN nodes n ON n.id = f.id
          WHERE {{CommonPredicateSql}}
          ORDER BY f.score DESC
          LIMIT @top_k;
          """;

    public override string BuildUpsertSql() =>
        """
        INSERT INTO nodes (
            id, document_id, document_group_id, version_number, tenant_id, app_id, collection_id, allowed_document_groups,
            fidelity_level, parent_id, logical_path, node_type, text, page, x, y, w, h, referenced_node_ids, vector
        )
        VALUES (
            @id, @document_id, @document_group_id, @version_number, @tenant_id, @app_id, @collection_id, @allowed_document_groups,
            @fidelity_level, @parent_id, @logical_path, @node_type, @text, @page, @x, @y, @w, @h, @referenced_node_ids, @vector
        )
        ON CONFLICT (id) DO UPDATE SET
            document_id = EXCLUDED.document_id,
            document_group_id = EXCLUDED.document_group_id,
            version_number = EXCLUDED.version_number,
            tenant_id = EXCLUDED.tenant_id,
            app_id = EXCLUDED.app_id,
            collection_id = EXCLUDED.collection_id,
            allowed_document_groups = EXCLUDED.allowed_document_groups,
            fidelity_level = EXCLUDED.fidelity_level,
            parent_id = EXCLUDED.parent_id,
            logical_path = EXCLUDED.logical_path,
            node_type = EXCLUDED.node_type,
            text = EXCLUDED.text,
            page = EXCLUDED.page,
            x = EXCLUDED.x,
            y = EXCLUDED.y,
            w = EXCLUDED.w,
            h = EXCLUDED.h,
            referenced_node_ids = EXCLUDED.referenced_node_ids,
            vector = EXCLUDED.vector;
        """;
}

internal sealed class PostgreSqlStorageSqlDialect : StorageSqlDialect
{
    public PostgreSqlStorageSqlDialect()
        : base(DatabaseEngine.PostgreSql)
    {
    }

    public override string BuildVectorQuerySql() =>
        $$"""
          SELECT id, document_id, node_type, text, 1 - (vector <=> @query_vector) AS _score, fidelity_level, page, x, y, w, h, logical_path, document_group_id, version_number, referenced_node_ids
          FROM nodes
          WHERE {{CommonPredicateSql}}
          ORDER BY vector <=> @query_vector
          LIMIT @top_k;
          """;

    public override string BuildTextQuerySql() =>
        $$"""
          SELECT id, document_id, node_type, text, ts_rank_cd(search_vector, websearch_to_tsquery('english', @query_text)) AS _score, fidelity_level, page, x, y, w, h, logical_path, document_group_id, version_number, referenced_node_ids
          FROM nodes
          WHERE {{CommonPredicateSql}}
            AND search_vector @@ websearch_to_tsquery('english', @query_text)
          ORDER BY _score DESC
          LIMIT @top_k;
          """;

    public override string BuildHybridQuerySql() =>
        $$"""
          WITH vector_hits AS (
              SELECT id, ROW_NUMBER() OVER (ORDER BY vector <=> @query_vector) AS v_rank
              FROM nodes
              WHERE {{CommonPredicateSql}}
              LIMIT @top_k
          ),
          text_hits AS (
              SELECT id, ROW_NUMBER() OVER (
                  ORDER BY ts_rank_cd(search_vector, websearch_to_tsquery('english', @query_text)) DESC
              ) AS t_rank
              FROM nodes
              WHERE {{CommonPredicateSql}}
                AND search_vector @@ websearch_to_tsquery('english', @query_text)
              LIMIT @top_k
          ),
          fused AS (
              SELECT id, SUM(rrf_score) AS score
              FROM (
                  SELECT id, (1.0 / (50 + v_rank)) AS rrf_score FROM vector_hits
                  UNION ALL
                  SELECT id, (1.0 / (50 + t_rank)) AS rrf_score FROM text_hits
              ) ranked
              GROUP BY id
          )
          SELECT n.id, n.document_id, n.node_type, n.text, f.score AS _score, n.fidelity_level, n.page, n.x, n.y, n.w, n.h, n.logical_path, n.document_group_id, n.version_number, n.referenced_node_ids
          FROM fused f
          INNER JOIN nodes n ON n.id = f.id
          WHERE {{CommonPredicateSql}}
          ORDER BY f.score DESC
          LIMIT @top_k;
          """;

    public override string BuildUpsertSql() => new CrateDbStorageSqlDialect().BuildUpsertSql();
}
